﻿/* 
 * Copyright (c) 2014, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.github.com/furore-fhir/sprinkler/master/LICENSE
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Furore.Fhir.Sprinkler.Framework.Framework;
using Furore.Fhir.Sprinkler.Framework.Framework.Framework;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;

namespace Furore.Fhir.Sprinkler.TestSet
{
    // Deprecated:
    // [SprinklerModule("DIGSIG")]
    public class DigSigTest : SprinklerTestClass
    {
        private string _signedXml;

        // [SprinklerTest("DS01", "Posting a feed with a valid signature")]
        public void TestSigning()
        {
            var b = new Bundle();

            b.Meta.LastUpdated = new DateTimeOffset(2012, 11, 2, 14, 17, 21, TimeSpan.Zero);
            b.Id = "urn:uuid:0d0dcca9-23b9-4149-8619-65002224c3";

            var p = new Patient();
            p.Id = "http://test.com/fhir/Patient/233";            
            p.Name = new List<HumanName> {HumanName.ForFamily("Kramer").WithGiven("Ewout")};

            var e = new Bundle.EntryComponent();
            e.Resource = p;
            b.Entry.Add(e);

            X509Certificate2 certificate = GetCertificate();

            byte[] bundleData = FhirSerializer.SerializeResourceToXmlBytes(b);            
            string bundleXml = Encoding.UTF8.GetString(bundleData);
            string bundleSigned = XmlSignatureHelper.Sign(bundleXml, certificate);
            _signedXml = bundleSigned;

            using (HttpWebResponse response = PostBundle(bundleSigned))
            {
                if (response.StatusCode != HttpStatusCode.OK)
                    Assert.Fail("Server refused POSTing signed document at /");
            }
        }

        private static X509Certificate2 GetCertificate()
        {
            Assembly myAssembly = typeof (CreateUpdateDeleteTest).Assembly;
            Stream stream = myAssembly.GetManifestResourceStream("Sprinkler.Tests.spark.pfx");
            var data = new byte[stream.Length];
            stream.Read(data, 0, (int) stream.Length);
            var certificate = new X509Certificate2(data);
            return certificate;
        }

        // [SprinklerTest("DS02", "Posting a feed with an invalid signature")]
        public void TestSigningTampered()
        {
            string bundleSigned = _signedXml;

            int dv = bundleSigned.IndexOf("<DigestValue>");
            string changedBundle = bundleSigned.Replace("<name>Ewout", "<name>Ewald");

            using (HttpWebResponse response = PostBundle(changedBundle))
            {
                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                    Assert.Fail("Server accepted POSTing an invalid and signed document at /");
            }
        }

        private HttpWebResponse PostBundle(string bundle)
        {
            WebRequest req = WebRequest.Create(Client.Endpoint);
            req.ContentType = "application/atom+xml";
            req.Method = "POST";
            Stream outStream = req.GetRequestStream();
            var outStreamWriter = new StreamWriter(outStream, Encoding.UTF8);

            outStreamWriter.Write(bundle);
            outStreamWriter.Flush();
            outStreamWriter.Close();

            var response = (HttpWebResponse) req.GetResponseNoEx();
            return response;
        }
    }
}