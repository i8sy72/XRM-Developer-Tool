﻿using System;
using System.Collections.Generic;
using System.Linq;
using JosephM.Core.Constants;
using JosephM.Core.Extentions;
using JosephM.Core.Log;
using JosephM.Core.Service;
using JosephM.Record.Extentions;
using JosephM.Record.IService;
using JosephM.Record.Metadata;
using JosephM.Record.Query;
using JosephM.Xrm.RecordExtract.DocumentWriter;
using JosephM.Xrm.RecordExtract.RecordExtract;

namespace JosephM.Xrm.RecordExtract.TextSearch
{
    public class TextSearchService :
        ServiceBase<TextSearchRequest, TextSearchResponse, TextSearchResponseItem>
    {
        public TextSearchService(IRecordService service,
            DocumentWriter.DocumentWriter documentWriter,
            RecordExtractService recordExtractService)
        {
            Service = service;
            DocumentWriter = documentWriter;
            RecordExtractService = recordExtractService;
        }

        private DocumentWriter.DocumentWriter DocumentWriter { get; set; }

        private RecordExtractService RecordExtractService { get; set; }

        private IRecordService Service { get; set; }

        public override void ExecuteExtention(TextSearchRequest request, TextSearchResponse response,
            LogController controller)
        {
            controller.UpdateProgress(0, 1, "Loading Text Search");
            var document = DocumentWriter.NewDocument();
            var firstSection = document.AddSection();
            var nextSection = document.AddSection();
            var container = new TextSearchContainer(request, response, controller, nextSection);
            ProcessRecordsContainedInName(container);
            ProcessRecordsReferencingTheWord(container);
            //insert title/summary
            firstSection.AddTitle("Text Search");
            var table = firstSection.Add2ColumnTable();
            table.AddFieldToTable("Execution Time", DateTime.Now.ToString(StringFormats.DateTimeFormat));
            table.AddFieldToTable("Search Operator", request.Operator.ToString());
            table.AddFieldToTable("Search Terms", string.Join(", ", request.SearchTerms.Select(s => "\"" + s.Text + "\"")));
            firstSection.AddTableOfContents(container.Bookmarks);

            //save document
            container.Controller.TurnOffLevel2();
            container.Controller.UpdateProgress(1, 2, "Creating Document");
            var folder = container.Request.SaveToFolder;
            var fileName = string.Format("Record Extract - {0} - {1}", "TextSearch",
                DateTime.Now.ToString("yyyyMMddHHmmss"));
            fileName = document.Save(folder, fileName, container.Request.DocumentFormat);
            container.Response.Folder = container.Request.SaveToFolder.FolderPath;
            container.Response.FileName = fileName;
        }

        //private void ProcessEntireRecordExtracts(TextSearchContainer container)
        //{
        //    //for each record with exact match on name do record extract
        //    //3. foreachrecord with name output them through recordextract
        //    var done = 0;
        //    var count = container.NameMatches.Count;
        //    var typesWithExactNameMatch = container.NameMatches.Select(r => r.Type).Distinct();
        //    var bookmark = container.AddHeadingWithBookmark("Detail of Records With Name Match");
        //    foreach (var type in typesWithExactNameMatch.OrderBy(Service.GetDisplayName))
        //    {
        //        var thisType = type;
        //        var thisBookmark = container.Section.AddHeading2WithBookmark(Service.GetCollectionName(thisType));
        //        bookmark.AddChildBookmark(thisBookmark);
        //        foreach (var record in container.NameMatches.Where(r => r.Type == thisType))
        //        {
        //            try
        //            {
        //                container.Controller.UpdateProgress(done++, count,
        //                    string.Format("Extracting Detail For {0} {1}", Service.GetDisplayName(type),
        //                        record.GetStringField(Service.GetPrimaryField(type))));
        //                var thisResponse =
        //                    RecordExtractService.ExtractRecordToDocument(container.Controller.GetLevel2Controller(),
        //                        record.ToLookup(),
        //                        container.Section, container.Request.DetailOfRecordsRelatedToMatches);
        //                container.Response.AddResponseItems(
        //                    thisResponse.ResponseItems.Select(r => new TextSearchResponseItem(r)));
        //                if (!thisResponse.Success)
        //                    throw thisResponse.Exception;
        //                thisBookmark.AddChildBookmarks(thisResponse.Bookmarks);
        //            }
        //            catch (Exception ex)
        //            {
        //                container.Response.AddResponseItem(new TextSearchResponseItem("Error Extracting Record",
        //                    thisType, ex));
        //            }
        //        }
        //    }
        //}

        private void ProcessRecordsReferencingTheWord(TextSearchContainer container)
        {
            //Search For String Field Matches
            //Append The Search For Records Referencing Records Which ZHad Name Matches
            //Then Output The Matching Fields From Those RecordTypes
            var bookmark = container.AddHeadingWithBookmark("Records With Field Matches");
            var recordTypes = GetSearchRecordTypes(container).ToArray();
            var count = recordTypes.Count();
            var done = 0;
            //load all the activity party references


            foreach (var recordType in recordTypes)
            {
                var recordsToOutput = new Dictionary<string, IRecord>();
                try
                {
                    AppendStringFieldMatches(container, recordType, done, count, recordsToOutput);
                    //if (container.Request.IncludeSearchReferenceFields)
                    //    AppendReferenceMatches(container, done, count, recordType, recordsToOutput);
                    AppendFieldMatchesToDocument(container, recordsToOutput, recordType, bookmark);
                }
                catch (Exception ex)
                {
                    container.Response.AddResponseItem(
                        new TextSearchResponseItem("Error Searching Entity Fields",
                            recordType, ex));
                }
                done++;
            }
        }

        private void AppendFieldMatchesToDocument(TextSearchContainer container,
            Dictionary<string, IRecord> recordsToOutput, string recordType,
            ContentBookmark bookmark)
        {
            Table2Column fieldCountTable = null;
            var fieldsDictionary = new Dictionary<string, int>();

            if (recordsToOutput.Any())
            {
                var recordOutput = false;
                var todoDone = 0;
                var todoCount = recordsToOutput.Count;
                var primaryField = Service.GetPrimaryField(recordType);

                var fieldsToExclude = GetFieldsToExlcude(container, recordType);

                //some lookup names dont get loaded into the record so will load them all now so i don't have to field by field
                recordsToOutput.Values.PopulateEmptyLookups(Service, ExtractUtility.GetSystemRecordTypesToExclude());

                var primaryFieldLabel = Service.GetFieldLabel(primaryField, recordType);
                var recordTypeCollectionLabel = Service.GetCollectionName(recordType);
                foreach (var match in recordsToOutput.Values)
                {
                    container.Controller.UpdateLevel2Progress(todoDone++, todoCount,
                        string.Format("Appending {0} To Document", recordTypeCollectionLabel));

                    var fieldsToDisplay = new List<string>();
                    foreach (
                        var field in
                            match.GetFieldsInEntity().Where(f => f != primaryField && !fieldsToExclude.Contains(f)))
                    {
                        if (Service.IsString(field, recordType))
                        {
                            var value = Service.GetFieldAsDisplayString(match, field);
                            if (container.Request.StripHtmlTagsPriorToSearch
                                && ExtractUtility.GetSystemHtmlFields().Where(rf => rf.RecordType.Key == recordType).Any(rf => rf.RecordField.Key == field))
                                value = value.StripHtml();
                            if (value != null)
                            {
                                if (IsSearchMatch(value, container))
                                {
                                    fieldsToDisplay.Add(field);
                                    if (!fieldsDictionary.ContainsKey(field))
                                        fieldsDictionary.Add(field, 0);
                                    fieldsDictionary[field]++;
                                }
                            }
                        }
                    }

                    if (fieldsToDisplay.Any())
                    {
                        if (!recordOutput)
                        {
                            var thisBookmark =
                                container.Section.AddHeading2WithBookmark(string.Format("{0} ({1})", Service.GetCollectionName(recordType), recordsToOutput.Count()));
                            bookmark.AddChildBookmark(thisBookmark);
                            recordOutput = true;
                            fieldCountTable = container.Section.Add2ColumnTable();
                        }
                        var table = container.Section.Add2ColumnTable();
                        table.AddFieldToTable(primaryFieldLabel, match.GetStringField(primaryField));
                        foreach (var field in fieldsToDisplay)
                        {
                            var value = Service.GetFieldAsDisplayString(match, field);
                            if (container.Request.StripHtmlTagsPriorToSearch
                                && ExtractUtility.GetSystemHtmlFields().Where(rf => rf.RecordType.Key == recordType).Any(rf => rf.RecordField.Key == field))
                                value = value.StripHtml();
                            if (value != null)
                            {
                                if (IsSearchMatch(value, container))
                                    table.AddFieldToTable(Service.GetFieldLabel(field, recordType),
                                        value);
                            }
                        }
                    }
                }
                //uh huh
                if (fieldsDictionary.Any())
                {
                    foreach (var field in fieldsDictionary
                        .OrderBy(f => Service.GetFieldLabel(f.Key, recordType)))
                    {

                        fieldCountTable.AddFieldToTable(Service.GetFieldLabel(field.Key, recordType), field.Value.ToString());
                    }
                }
            }
        }

        private List<string> GetFieldsToExlcude(TextSearchContainer container, string recordType)
        {
            var fieldsToExclude = new List<string>();
            fieldsToExclude.AddRange(ExtractUtility.GetSystemFieldsToExclude());
            if (container.Request.FieldExclusions != null)
            {
                fieldsToExclude.AddRange(container.Request.FieldExclusions.Where(fe => fe.RecordType.Key == recordType).Select(fe => fe.RecordField.Key));
            }

            foreach (var field in Service.GetFields(recordType))
            {
                var fieldType = Service.GetFieldType(field, recordType);
                if (fieldType == RecordFieldType.Uniqueidentifier)
                    fieldsToExclude.Add(field);
                else if (Service.GetFieldType(field, recordType) == RecordFieldType.String &&
                         Service.GetFieldMetadata(field, recordType).TextFormat == TextFormat.PhoneticGuide)
                    fieldsToExclude.Add(field);
                if (field.EndsWith("_base") && fieldType == RecordFieldType.Money)
                    fieldsToExclude.Add(field);
            }

            return fieldsToExclude;
        }

        private void AppendStringFieldMatches(TextSearchContainer container, string recordType, int done, int count,
            Dictionary<string, IRecord> recordsToOutput)
        {
            var primaryField = Service.GetPrimaryField(recordType);
            var thisRecordType = recordType;
            container.Controller.UpdateProgress(done, count,
                string.Format("Searching String Fields In {0}", Service.GetCollectionName(recordType)));
            try
            {
                var fieldsToExclude = GetFieldsToExlcude(container, recordType);

                var nonPrimaryStringFields = Service.GetFields(recordType)
                    .Where(f => Service.IsString(f, recordType))
                    .Where(f => f != primaryField)
                    .Where(f => Service.IsString(f, thisRecordType))
                    .Except(fieldsToExclude)
                    .ToArray();

                if (nonPrimaryStringFields.Any())
                {


                    var htmlSearchFields = container.Request.StripHtmlTagsPriorToSearch
                        ? nonPrimaryStringFields
                        .Intersect((ExtractUtility.GetSystemHtmlFields())
                        .Where(rf => rf.RecordType.Key == recordType)
                        .Select(rf => rf.RecordField.Key)).ToArray()
                        : new string[0];
                    var setSearchFields = nonPrimaryStringFields
                        .Intersect(ExtractUtility.GetSystemTextSearchSetFields()
                        .Where(f => f.RecordType.Key == recordType)
                        .Select(f => f.RecordField.Key)
                        .Except(htmlSearchFields))
                        .ToArray();
                    var nonSetSearchFields = nonPrimaryStringFields
                        .Except(setSearchFields)
                        .Except(htmlSearchFields)
                        .ToArray();



                    if (htmlSearchFields.Any())
                    {
                        //this code written as the crm web service / sql timedout when doing text searches over the entire record table
                        //i thus split all the records into sets defined by a date range and query the text in each set iteratively
                        //this way I limit the volume of text being searched in each crm web service query by a approximate number of records defined in the settings
                        container.Controller.UpdateLevel2Progress(0, 1, string.Format("Configuring Html Search Sets"));
                        int totalCount = 0;
                        var sortedDatesTemplate = GetDateRangesForSetSearches(recordType, out totalCount);
                        var totalDone = 0;
                        foreach (var field in htmlSearchFields)
                        {
                            try
                            {
                                var thisFieldSortedDates = sortedDatesTemplate.ToList();

                                var label = Service.GetFieldLabel(field, recordType);

                                //now query the text in each date range set
                                while (thisFieldSortedDates.Any())
                                {
                                    var first = thisFieldSortedDates.First();
                                    var limit = thisFieldSortedDates.Count > 1 ? thisFieldSortedDates[1] : (DateTime?)null;
                                    if (first.Equals(limit) && thisFieldSortedDates.Any(l => l > first))
                                    {
                                        limit = thisFieldSortedDates.First(l => l > first);
                                    }

                                    var query = new QueryDefinition(recordType);
                                    query.RootFilter = new Filter();
                                    query.RootFilter.ConditionOperator = container.Request.Operator == TextSearchRequest.SearchTermOperator.And ? FilterOperator.And : FilterOperator.Or;
                                    query.RootFilter.SubFilters = container.Request.SearchTerms.Select(s =>
                                    {
                                        var searchTermFilter = new Filter();
                                        searchTermFilter.Conditions = new List<Condition>();
                                        searchTermFilter.Conditions.Add(new Condition("createdon", ConditionType.GreaterEqual, first));
                                        if(limit.HasValue)
                                            searchTermFilter.Conditions.Add(new Condition("createdon", ConditionType.LessThan, limit.Value));
                                        return searchTermFilter;
                                    }).ToList();


                                    var allOfType = Service.RetreiveAll(query);
                                    foreach (var item in allOfType)
                                    {
                                        container.Controller.UpdateLevel2Progress(totalDone++, totalCount, string.Format("Searching Html {0}", label));
                                        var fieldValue = item.GetStringField(field);
                                        if(fieldValue != null)
                                        {
                                            var stripHtml = fieldValue.StripHtml();
                                            if(IsSearchMatch(stripHtml, container))
                                                recordsToOutput.Add(item.Id, item);
                                        }
                                    }
                                    thisFieldSortedDates.RemoveAt(0);
                                }
                            }
                            catch (Exception ex)
                            {
                                container.Response.AddResponseItem(
                                    new TextSearchResponseItem("Error Searching String Fields", recordType, field, ex));
                            }
                        }
                    }

                    var fieldsTodo = nonSetSearchFields.Count();
                    var fieldsDone = 0;
                    foreach (var field in nonSetSearchFields)
                    {
                        container.Controller.UpdateLevel2Progress(fieldsDone++, fieldsTodo, "Searching String Fields");
                        try
                        {
                            var conditions =
                                container.Request.SearchTerms.Select(s => 
                                 new Condition(field, ConditionType.Like, string.Format("%{0}%", s.Text)))
                                 .ToArray();
                            var stringFieldMatches = (container.Request.Operator == TextSearchRequest.SearchTermOperator.And
                                ? Service.RetrieveAllOrClauses(recordType, conditions, null)
                                : Service.RetrieveAllOrClauses(recordType, conditions, null))
                                .ToArray();
                            foreach (var stringFieldMatch in stringFieldMatches)
                            {
                                if (!recordsToOutput.ContainsKey(stringFieldMatch.Id))
                                    recordsToOutput.Add(stringFieldMatch.Id, stringFieldMatch);
                            }
                        }
                        catch (Exception ex)
                        {
                            container.Response.AddResponseItem(
                                new TextSearchResponseItem("Error Searching String Fields", recordType, field, ex));
                        }
                    }
                    if (setSearchFields.Any())
                    {
                        //this code written as the crm web service / sql timedout when doing text searches over the entire record table
                        //i thus split all the records into sets defined by a date range and query the text in each set iteratively
                        //this way I limit the volume of text being searched in each crm web service query by a approximate number of records defined in the settings
                        container.Controller.UpdateLevel2Progress(0, 1, string.Format("Configuring Search Sets"));
                        var fieldSetsTodo = 0;
                        var sortedDatesTemplate = GetDateRangesForSetSearches(recordType, out fieldSetsTodo);
                        foreach (var field in setSearchFields)
                        {
                            try
                            {
                                var thisFieldSortedDates = sortedDatesTemplate.ToList();
                                var fieldSetsDone = 0;
                                var label = Service.GetFieldLabel(field, recordType);
                                //now query the text in each date range set
                                while (thisFieldSortedDates.Any())
                                {
                                    container.Controller.UpdateLevel2Progress(fieldSetsDone + ExtractUtility.TextSearchSetSize, fieldSetsTodo,
                                        string.Format("Searching {0}", label));
                                    var first = thisFieldSortedDates.First();
                                    var limit = thisFieldSortedDates.Count > 1 ? thisFieldSortedDates[1] : (DateTime?)null;
                                    var query = new QueryDefinition(recordType);
                                    query.RootFilter = new Filter();
                                    query.RootFilter.ConditionOperator = container.Request.Operator == TextSearchRequest.SearchTermOperator.And ? FilterOperator.And : FilterOperator.Or;
                                    query.RootFilter.SubFilters = container.Request.SearchTerms.Select(s =>
                                    {
                                        var searchTermFilter = new Filter();
                                        searchTermFilter.Conditions = new List<Condition>();
                                        searchTermFilter.Conditions.Add(new Condition("createdon", ConditionType.GreaterEqual, first));
                                        if(limit.HasValue)
                                            searchTermFilter.Conditions.Add(new Condition("createdon", ConditionType.LessThan, limit.Value));
                                        return searchTermFilter;
                                    }).ToList();


                                    var stringFieldMatches = Service.RetreiveAll(query);
                                    foreach (var stringFieldMatch in stringFieldMatches)
                                    {
                                        recordsToOutput.Add(stringFieldMatch.Id, stringFieldMatch);
                                    }
                                    thisFieldSortedDates.RemoveAt(0);
                                }
                            }
                            catch (Exception ex)
                            {
                                container.Response.AddResponseItem(
                                    new TextSearchResponseItem("Error Searching String Fields", recordType, field, ex));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                container.Response.AddResponseItem(
                    new TextSearchResponseItem("Error Searching String Fields",
                        recordType, ex));
            }
        }

        private List<DateTime> GetDateRangesForSetSearches(string recordType, out int totalCount)
        {
            totalCount = 0;
            var startDate = new DateTime(1901, 01, 01);
            var sortedDatesTemplate = new List<DateTime>();
            //query the created dates of all records in the table
            //this does iterative queries sorting by created date
            //to avoid crm requerying the entire table for each iterative request
            while (true)
            {
                var records =
                    Service.GetFirstX(recordType, ExtractUtility.TextSearchSetSize, new[] { "createdon" },
                        new[]
                                {
                                     new Condition("createdon", ConditionType.GreaterThan, startDate)
                                },
                        new[] { new SortExpression("createdon", SortType.Ascending) }).ToArray();
                totalCount = totalCount + records.Count();
                if (!records.Any())
                    break;
                var theseDates =
                    records.Where(r => r.GetDateTime("createdon").HasValue)
                        .Select(r => r.GetDateTime("createdon"))
                        .Cast<DateTime>()
                        .ToList();
                theseDates.Sort();
                sortedDatesTemplate.Add(startDate);
                startDate = theseDates.First();
                if (records.Count() < ExtractUtility.TextSearchSetSize)
                    break;
            }
            sortedDatesTemplate.Sort();
            return sortedDatesTemplate;
        }

        //private void AppendReferenceMatches(TextSearchContainer container, int done, int count, string recordType,
        //    Dictionary<string, IRecord> recordsToOutput)
        //{
        //    try
        //    {
        //        var progressPrefix = string.Format("Searching Reference Fields In {0}",
        //            Service.GetCollectionName(recordType));
        //        container.Controller.UpdateProgress(done, count, progressPrefix);
        //        var recordTypesWithNameMatch = container.GetRecordTypesWithNameMatch().ToArray();
        //        var oneToManyRelationships =
        //            recordTypesWithNameMatch
        //                .SelectMany(r => Service.GetOneToManyRelationships(r))
        //                .Where(r => r.ReferencingEntity == recordType)
        //                .ToArray();
        //        var level2Done = 0;
        //        var level2Count = oneToManyRelationships.Count();

        //        // get the activity party references
        //        if (Service.GetRecordTypeMetadata(recordType).IsActivityType)
        //        {
        //            var activityPartyReferences = new List<IRecord>();
        //            //need to the activities which have an activity party match
        //            foreach (var match in container.NameMatches)
        //            {
        //                if (Service.GetRecordTypeMetadata(match.Type).IsActivityParticipant)
        //                {
        //                    var conditions = new[]
        //                    {
        //                        new Condition("partyid", ConditionType.Equal, match.Id)
        //                    };
        //                    //need conditions where the party is a type match and the activity is this type
        //                    //simpler just get for all types inititally
        //                    var activityParties = Service.RetrieveAllAndClauses(
        //                        "activityparty",
        //                        conditions
        //                        , null);
        //                    activityPartyReferences.AddRange(
        //                        activityParties.Where(ap => ap.GetLookupType("partyid") == match.Type));
        //                }
        //            }
        //            if (activityPartyReferences.Any())
        //            {
        //                var conditions = activityPartyReferences
        //                    .Select(
        //                        ap =>
        //                            new Condition(Service.GetPrimaryKey(recordType), ConditionType.Equal,
        //                                ap.GetLookupId("activityid")));
        //                var activities = Service.RetrieveAllOrClauses(recordType, conditions, null);
        //                foreach (var activity in activities)
        //                {
        //                    if (!recordsToOutput.ContainsKey(activity.Id))
        //                        recordsToOutput.Add(activity.Id, activity);
        //                }
        //            }
        //        }

        //        foreach (var recordTypeWithNameMatch in recordTypesWithNameMatch)
        //        {
        //            var thisRecordTypeNameMatch = recordTypeWithNameMatch;

        //            foreach (
        //                var one2ManyRelationshipMetadata in
        //                    oneToManyRelationships.Where(r => r.ReferencedEntity == thisRecordTypeNameMatch))
        //            {
        //                var thisMetadata = one2ManyRelationshipMetadata;
        //                try
        //                {
        //                    container.Controller.UpdateLevel2Progress(level2Done++, level2Count,
        //                        string.Format("Searching {0} {1}",
        //                            Service.GetFieldLabel(one2ManyRelationshipMetadata.ReferencingAttribute,
        //                                one2ManyRelationshipMetadata.ReferencingEntity),
        //                            Service.GetDisplayName(one2ManyRelationshipMetadata.ReferencedEntity)));
        //                    var conditions = container.NameMatches
        //                        .Where(r => r.Type == thisRecordTypeNameMatch)
        //                        .Select(
        //                            m =>
        //                                new Condition(thisMetadata.ReferencingAttribute,
        //                                    ConditionType.Equal,
        //                                    m.Id));
        //                    var relatedEntities = Service.RetrieveAllOrClauses(recordType, conditions, null);
        //                    foreach (var relatedEntity in relatedEntities)
        //                    {
        //                        if (!recordsToOutput.ContainsKey(relatedEntity.Id))
        //                            recordsToOutput.Add(relatedEntity.Id, relatedEntity);
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    container.Response.AddResponseItem(
        //                        new TextSearchResponseItem("Error Searching Reference Fields", recordType,
        //                            one2ManyRelationshipMetadata.ReferencingAttribute, ex));
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        container.Response.AddResponseItem(
        //            new TextSearchResponseItem("Error Searching Reference Fields",
        //                recordType, ex));
        //    }
        //}

        private void ProcessRecordsContainedInName(TextSearchContainer container)
        {
            var bookmark = container.AddHeadingWithBookmark("Records With Name Match");
            var recordTypes = GetSearchRecordTypes(container).ToArray();
            var count = recordTypes.Count();
            var done = 0;
            foreach (var recordType in recordTypes)
            {
                try
                {
                    var progressTextPrefix = string.Format("Searching Names In {0}",
                        Service.GetCollectionName(recordType));
                    container.Controller.UpdateProgress(done++, count, progressTextPrefix);
                    var primaryField = Service.GetPrimaryField(recordType);
                    if (!primaryField.IsNullOrWhiteSpace())
                    {
                        var conditions =
                            container.Request.SearchTerms.Select(s =>
                             new Condition(primaryField, ConditionType.Like, string.Format("%{0}%", s.Text)))
                             .ToArray();
                        var matches = (container.Request.Operator == TextSearchRequest.SearchTermOperator.And
                            ? Service.RetrieveAllOrClauses(recordType, conditions, null)
                            : Service.RetrieveAllOrClauses(recordType, conditions, null))
                            .ToArray();
                        if (matches.Any())
                        {
                            try
                            {
                                var thisBookmark =
                                    container.Section.AddHeading2WithBookmark(string.Format("{0} ({1})", Service.GetCollectionName(recordType), matches.Count()));
                                bookmark.AddChildBookmark(thisBookmark);
                                var table = container.Section.Add1ColumnTable();
                                var matchCount = matches.Count();
                                var matchCountDone = 0;
                                foreach (var match in matches)
                                {
                                    container.Controller.UpdateProgress(done, count,
                                        string.Format("{0} (Adding {1} Of {2})", progressTextPrefix, ++matchCountDone,
                                            matchCount));
                                    container.AddNameMatch(match);
                                    var outputText = match.GetStringField(primaryField);
                                    outputText = ExtractUtility.CheckStripFormatting(outputText, recordType,
                                        primaryField);
                                    table.AddRow(outputText);
                                }
                            }
                            catch (Exception ex)
                            {
                                container.Response.AddResponseItem(
                                    new TextSearchResponseItem("Error Adding Matched Record",
                                        recordType, ex));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    container.Response.AddResponseItem(new TextSearchResponseItem("Error Adding Match Records",
                        recordType, ex));
                }
            }
        }

        private IEnumerable<string> GetSearchRecordTypes(TextSearchContainer container)
        {
            var typesToSearch = new List<string>();
            if (!container.Request.SearchAllTypes)
            {
                typesToSearch.AddRange(container.Request.TypesToSearch.Select(r => r.RecordType.Key));
            }
            else
            {

                typesToSearch.AddRange(Service.GetAllRecordTypes().Where(r => Service.GetRecordTypeMetadata(r).Searchable));
                var systemExcludes = ExtractUtility.GetSystemRecordTypesToExclude();
                if(systemExcludes != null)
                {
                    typesToSearch = typesToSearch.Except(systemExcludes).ToList();
                }
                if (container.Request.OtherExclusions != null)
                {
                    typesToSearch = typesToSearch.Except(container.Request.OtherExclusions.Select(r => r.RecordType.Key)).ToList();
                }
                if (container.Request.ExcludeEmails)
                    typesToSearch.RemoveAll(s => s == "email");
                if (container.Request.ExcludeEmails)
                    typesToSearch.RemoveAll(s => s == "post");
            }
            return typesToSearch.OrderBy(n => Service.GetDisplayName(n)).ToArray();
        }

        internal bool IsSearchMatch(string stringValue, TextSearchContainer container)
        {
            var match = false;
            if(stringValue != null)
            {
                foreach(var term in container.Request.SearchTerms)
                {
                    if (stringValue.ToLower().Contains(term.Text.ToLower()))
                    {
                        match = true;
                        break;
                    }
                }
            }
            return match;
        }
    }
}