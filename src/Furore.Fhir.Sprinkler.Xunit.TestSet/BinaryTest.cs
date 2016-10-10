﻿using System;
using Furore.Fhir.Sprinkler.FhirUtilities.ResourceManagement;
using Furore.Fhir.Sprinkler.XunitRunner.FhirExtensions;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Xunit;
using System.Linq;
using System.Net;
using Furore.Fhir.Sprinkler.Xunit.ClientUtilities;
using Furore.Fhir.Sprinkler.Xunit.ClientUtilities.XunitFhirExtensions.Attributes;
using Furore.Fhir.Sprinkler.Xunit.ClientUtilities.XunitFhirExtensions.ClassFixtures;

namespace Furore.Fhir.Sprinkler.Xunit.TestSet
{
    [FixtureConfiguration(FixtureType.File)]
    [TestCaseOrderer("Furore.Fhir.Sprinkler.Xunit.ClientUtilities.XunitFhirExtensions.TestCaseOrderers.PriorityOrderer", "Furore.Fhir.Sprinkler.Xunit.ClientUtilities")]
    public class BinaryTest : IClassFixture<TestDependencyContext<Binary>>, IClassFixture<FhirClientFixture>
    {
        private readonly TestDependencyContext<Binary> dependencyContext;
        private readonly FhirClient client;

        public BinaryTest(TestDependencyContext<Binary> dependencyContext, FhirClientFixture client)
        {
            this.dependencyContext = dependencyContext;
            this.client = client.Client;
        }

        [Theory, TestPriority(0)]
        [TestMetadata("BI01", "Create a binary")]
        [Fixture(false, "patient-example.xml")]
        public void CreateBinary(Patient patient)
        {
            client.PreferredFormat = ResourceFormat.Xml;
            client.ReturnFullResource = true;
            dependencyContext.Dependency = GetPhotoBinary(patient);

            Binary received = client.Create(dependencyContext.Dependency);

            CheckBinary(received);
            dependencyContext.Dependency = received;
        }

        [Fact, TestPriority(1)]
        [TestMetadata("BI02", "Read binary as xml")]
        public void ReadBinaryAsXml()
        {
            if (dependencyContext.Dependency == null) FhirAssert.Skip();

            client.PreferredFormat = ResourceFormat.Xml;
            client.UseFormatParam = true;
            client.ReturnFullResource = true;

            Binary result = client.Read<Binary>(dependencyContext.Id);
            FhirAssert.ResourceResponseConformsTo(client, client.PreferredFormat);

            CheckBinary(result);
            dependencyContext.Dependency = result;
        }

        [Fact, TestPriority(2)]
        [TestMetadata("BI03", "Read binary as json")]
        public void ReadBinaryAsJson()
        {
            if (dependencyContext.Dependency == null) FhirAssert.Skip();

            client.PreferredFormat = ResourceFormat.Json;
            client.UseFormatParam = false;
            client.ReturnFullResource = true;

            Binary result = client.Read<Binary>(dependencyContext.Id);
            FhirAssert.ResourceResponseConformsTo(client, client.PreferredFormat);

            CheckBinary(result);
            dependencyContext.Dependency = result;
        }

        [Fact(Skip = "Known issue: FHIR.API doesn't send binary resources in a resource envelope and the documentation is not clear if FHIR servers should accept it like that."), TestPriority(3)]
        [TestMetadata("BI04", "Update binary")]
        public void UpdateBinary()
        {
            if (dependencyContext.Dependency == null) FhirAssert.Skip();

            dependencyContext.Dependency.Content = dependencyContext.Dependency.Content.Reverse().ToArray();
            Binary result = client.Update(dependencyContext.Dependency);

            CheckBinary(result);
        }

        [TestMetadata("BI05", "Delete binary")]
        [Fact, TestPriority(4)]

        public void DeleteBinary()
        {
            if (dependencyContext.Dependency == null) FhirAssert.Skip();

            client.Delete(dependencyContext.Id);

            FhirAssert.Fails(client, () => client.Read<Binary>(dependencyContext.Id), HttpStatusCode.Gone);
        }

        private Binary GetPhotoBinary(Patient patient)
        {
            var bin = new Binary();

            // NB: in the default patient-example there is no photo element.
            // Copy the photo element from the current example when replacing this file!
            bin.Content = patient.Photo[0].Data;

            bin.ContentType = patient.Photo[0].ContentType;

            return bin;
        }

        private void CheckBinary(Binary result)
        {
            FhirAssert.LocationPresentAndValid(client);
            FhirAssert.IsTrue(dependencyContext.Dependency.ContentType == result.ContentType, "ContentType of the received binary is not correct");
            CompareData(dependencyContext.Dependency.Content, result);

        }

        private static void CompareData(byte[] data, Binary received)
        {
            if (data.Length != received.Content.Length)
                FhirAssert.Fail("Binary data returned has a different size");
            for (int pos = 0; pos < data.Length; pos++)
                if (data[pos] != received.Content[pos])
                    FhirAssert.Fail("Binary data returned differs from original");
        }

     
    }
}