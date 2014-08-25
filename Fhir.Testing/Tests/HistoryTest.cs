﻿/* 
 * Copyright (c) 2014, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.github.com/furore-fhir/sprinkler/master/LICENSE
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Sprinkler.Framework;

namespace Sprinkler.Tests
{
    [SprinklerTestModule("History")]
    public class HistoryTest : SprinklerTestClass
    {
        //private CreateUpdateDeleteTest crudTests;
        private readonly List<Uri> Versions = new List<Uri>();
        private DateTimeOffset? _createDate;
        private int _forwardCount = -1;
        private Bundle _history;

        private string _id;
        private Bundle _lastPage;
        private string _location;
        private Bundle _systemHistory;
        private Bundle _typeHistory;

        private void Initialize()
        {
            _id = "sprink" + new Random().Next();
            _location = "Patient/" + _id;
            _createDate = DateTimeOffset.Now;

            Patient patient = DemoData.GetDemoPatient();
            ResourceEntry<Patient> entry;

            entry = Client.Create(patient, _id, null, true);
            Versions.Add(entry.SelfLink);

            entry.Resource.Telecom.Add(new Contact {System = Contact.ContactSystem.Email, Value = "info@furore.com"});

            entry = Client.Update(entry, true);
            Versions.Add(entry.SelfLink);

            entry = Client.Update(entry, true);
            Versions.Add(entry.SelfLink);

            Client.Delete(_location);
        }


        [SprinklerTest("HI01", "Request the full history for a specific resource")]
        public void HistoryForSpecificResource()
        {
            Initialize();
            if (_createDate == null) TestResult.Skip();

            _history = Client.History(_location);
            HttpTests.AssertEntryIdsArePresentAndAbsoluteUrls(_history);

            // There's one version less here, because we don't have the deletion
            int expected = Versions.Count + 1;

            if (_history.Entries.Count != expected)
                TestResult.Fail(String.Format("{0} versions expected after crud test, found {1}", expected,
                    _history.Entries.Count));

            if (!_history.Entries.OfType<ResourceEntry>()
                .All(ent => Versions.Contains(ent.SelfLink)))
                TestResult.Fail("Selflinks on returned versions do not match links returned on creation" +
                                _history.Entries.Count);


            CheckSortOrder(_history);
        }


        [SprinklerTest("HI02", "Request the full history for a resource with _since")]
        public void HistoryForSpecificResourceId()
        {
            if (_createDate == null) TestResult.Skip();

            DateTimeOffset before = _createDate.Value.AddMinutes(-1);
            DateTimeOffset after = before.AddHours(1);

            Bundle history = Client.History(_location, before);
            HttpTests.AssertEntryIdsArePresentAndAbsoluteUrls(history);
            CheckSortOrder(history);
            Uri[] historySl = history.Entries.Select(entry => entry.SelfLink).ToArray();

            if (!history.Entries.All(entry => historySl.Contains(entry.SelfLink)))
                TestResult.Fail("history with _since does not contain all versions of instance");

            history = Client.History(_location, after);
            if (history.Entries.Count != 0)
                TestResult.Fail("Setting since to after the last update still returns history");
        }


        [SprinklerTest("HI03", "Request individual history versions from a resource")]
        public void VReadVersions()
        {
            foreach (ResourceEntry ent in _history.Entries.OfType<ResourceEntry>())
            {
                var identity = new ResourceIdentity(ent.SelfLink);

                ResourceEntry<Patient> version = Client.Read<Patient>(identity);

                if (version == null) TestResult.Fail("Cannot find version that was present in history");

                HttpTests.AssertContentLocationPresentAndValid(Client);
                var selfLink = new ResourceIdentity(Client.LastResponseDetails.ContentLocation);
                if (String.IsNullOrEmpty(selfLink.Id) || String.IsNullOrEmpty(selfLink.VersionId))
                    TestResult.Fail("Optional Content-Location contains an invalid version-specific url");
            }

            foreach (DeletedEntry ent in _history.Entries.OfType<DeletedEntry>())
            {
                var identity = new ResourceIdentity(ent.SelfLink);

                HttpTests.AssertFail(Client, () => Client.Read<Patient>(identity), HttpStatusCode.Gone);
            }
        }

        [SprinklerTest("HI04", "Fetching history of non-existing resource returns exception")]
        public void HistoryForNonExistingResource()
        {
            if (_createDate == null) TestResult.Skip();

            HttpTests.AssertFail(Client, () => Client.History("Patient/3141592unlikely"), HttpStatusCode.NotFound);
        }

        // todo: bug in API. Werkt weer bij volgende versie.
        [SprinklerTest("HI06", "Get all history for a resource type with _since")]
        public void HistoryForResourceType()
        {
            if (_createDate == null) TestResult.Skip();

            DateTimeOffset before = _createDate.Value.AddMinutes(-1);
            DateTimeOffset after = before.AddHours(1);

            Bundle history = Client.TypeHistory<Patient>(before);
            HttpTests.AssertEntryIdsArePresentAndAbsoluteUrls(history);
            _typeHistory = history;
            CheckSortOrder(history);

            Uri[] historyLinks = history.Entries.Select(be => be.SelfLink).ToArray();

            if (!history.Entries.All(ent => historyLinks.Contains(ent.SelfLink)))
                TestResult.Fail("history with _since does not contain all versions of instance");

            history = Client.History(_location, DateTimeOffset.Now.AddMinutes(1));

            if (history.Entries.Count != 0)
                TestResult.Fail("Setting since to a future moment still returns history");
        }


        [SprinklerTest("HI08", "Get the history for the whole system with _since")]
        public void HistoryForWholeSystem()
        {
            if (_createDate == null) TestResult.Skip();
            DateTimeOffset before = _createDate.Value.AddMinutes(-1);
            Bundle history;


            history = Client.WholeSystemHistory(before);
            HttpTests.AssertEntryIdsArePresentAndAbsoluteUrls(history);

            HttpTests.AssertHasAllForwardNavigationLinks(history); // Assumption: system has more history than pagesize
            _systemHistory = history;
            CheckSortOrder(history);

            Uri[] historyLinks = history.Entries.Select(be => be.SelfLink).ToArray();

            if (!Versions.All(sl => historyLinks.Contains(sl)))
                TestResult.Fail("history with _since does not contain all versions of instance");

            history = Client.History(_location, DateTimeOffset.Now.AddMinutes(1));

            if (history.Entries.Count != 0)
                TestResult.Fail("Setting since to a future moment still returns history");
        }

        [SprinklerTest("HI09", "Paging forward through a resource type history")]
        public void PageFwdThroughResourceHistory()
        {
            int pageSize = 30;
            Bundle page = Client.TypeHistory<Patient>(pageSize: pageSize);
            HttpTests.AssertEntryIdsArePresentAndAbsoluteUrls(page);

            _forwardCount = 0;

            // Browse forwards
            while (page != null)
            {
                if (page.Entries.Count > pageSize)
                    TestResult.Fail("Server returned a page with more entries than set by _count");

                _forwardCount += page.Entries.Count;
                _lastPage = page;
                HttpTests.AssertEntryIdsArePresentAndAbsoluteUrls(page);
                page = Client.Continue(page);
            }

            //if (total.HasValue && forwardCount < total)
            //    TestResult.Fail(String.Format("Paging did not return all entries(expected at least {0}, {1} returned)", 
            //                    total, forwardCount));
        }

        [SprinklerTest("HI10", "Page backwards through a resource type history")]
        public void PageBackThroughResourceHistory()
        {
            if (_forwardCount == -1) TestResult.Skip();

            int pageSize = 30;
            Bundle page = Client.TypeHistory<Patient>(pageSize: pageSize);
            HttpTests.AssertEntryIdsArePresentAndAbsoluteUrls(page);

            page = Client.Continue(page, PageDirection.Last);
            int backwardsCount = 0;

            // Browse backwards
            while (page != null)
            {
                if (page.Entries.Count > pageSize)
                    TestResult.Fail("Server returned a page with more entries than set by count");

                backwardsCount += page.Entries.Count;
                HttpTests.AssertEntryIdsArePresentAndAbsoluteUrls(page);
                page = Client.Continue(page, PageDirection.Previous);
            }

            if (backwardsCount != _forwardCount)
                TestResult.Fail(String.Format("Paging forward returns {0} entries, backwards returned {1}",
                    _forwardCount, backwardsCount));
        }

        [SprinklerTest("HI11", "Fetch first page of full histroy")]
        public void FullHistory()
        {
            Bundle history = Client.WholeSystemHistory();
        }


        private static void CheckSortOrder(Bundle bundle)
        {
            DateTimeOffset maxDate = DateTimeOffset.MaxValue;

            foreach (BundleEntry be in bundle.Entries)
            {
                DateTimeOffset? lastUpdate = be is ResourceEntry
                    ? ((be as ResourceEntry).LastUpdated)
                    : ((be as DeletedEntry).When);

                if (lastUpdate == null)
                    TestResult.Fail(String.Format("Result contains entry with no LastUpdate (id: {0})", be.SelfLink));

                if (lastUpdate > maxDate)
                    TestResult.Fail("Result is not ordered on LastUpdate, first out of order has id " + be.SelfLink);

                maxDate = lastUpdate.Value;
            }
        }
    }
}