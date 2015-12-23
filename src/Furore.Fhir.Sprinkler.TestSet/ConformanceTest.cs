﻿/* 
 * Copyright (c) 2014, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.github.com/furore-fhir/sprinkler/master/LICENSE
 */

using Furore.Fhir.Sprinkler.Framework.Framework;
using Hl7.Fhir.Model;

namespace Furore.Fhir.Sprinkler.TestSet
{
    [SprinklerModule("Conformance")]
    public class ConformanceTest : SprinklerTestClass
    {
        [SprinklerTest("CN01", "Request conformance on /metadata")]
        public void GetConformanceUsingMetadata()
        {
            //ResourceEntry<Conformance> entry = Client.Conformance(useOptionsVerb: false);
            Conformance Conformance = Client.Conformance();
            CheckResultHeaders();
        }

        //[SprinklerTest("CN02", "Request conformance using OPTIONS")]
        //public void GetConformanceUsingOptions()
        //{
        //    //Client.Conformance(true);
        //    CheckResultHeaders();
        //}

        private void CheckResultHeaders()
        {
            Assert.ValidResourceContentTypePresent(Client);
            Assert.ContentLocationValidIfPresent(Client);
        }
    }
}