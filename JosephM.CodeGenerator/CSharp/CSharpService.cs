﻿using JosephM.Application.Application;
using JosephM.Core.Log;
using JosephM.Core.Service;
using JosephM.Core.Utility;
using JosephM.Record.Extentions;
using JosephM.Record.Query;
using JosephM.Record.Xrm.XrmRecord;
using JosephM.Xrm;
using JosephM.Xrm.Schema;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace JosephM.CodeGenerator.CSharp
{
    public class CSharpService : CodeGeneratorServiceBase<CSharpRequest, CSharpResponse, ServiceResponseItem>
    {
        public IApplicationController ApplicationController { get; }

        public CSharpService(XrmRecordService service, IApplicationController applicationController)
            : base(service)
        {
            ApplicationController = applicationController;
        }

        public override void ExecuteExtention(CSharpRequest request, CSharpResponse response,
            ServiceRequestController controller)
        {
            WriteCSharpMetadata(request, controller.Controller, response);
        }

        private void AppendGenerationComments(StringBuilder sb)
        {
            sb.AppendLine("/*");
            sb.AppendLine($"This code was generated by '{ApplicationController.ApplicationName}'");
            sb.AppendLine("https://github.com/josephmcmac/XRM-Developer-Tool");
            sb.AppendLine("*/");
        }

        private void WriteCSharpMetadata(CSharpRequest request, LogController controller, CSharpResponse response)
        {
            var stringBuilder = new StringBuilder();
            AppendGenerationComments(stringBuilder);
            stringBuilder.AppendLine("namespace " + request.Namespace);
            stringBuilder.AppendLine("{");
            controller.LogLiteral("Loading Types.....");
            AppendEntities(controller, request, stringBuilder);
            AppendRelationships(controller, request, stringBuilder);
            AppendFields(controller, stringBuilder, request);
            AppendOptionSets(stringBuilder, request, controller);
            AppendActions(stringBuilder, request, controller);
            stringBuilder.AppendLine("}");
            var fileName = request.FileName.EndsWith(".cs") ? request.FileName : request.FileName + ".cs";
            FileUtility.WriteToFile(request.Folder.FolderPath, fileName, stringBuilder.ToString());
            response.CSharpCode = stringBuilder.ToString();
            response.Folder = request.Folder?.FolderPath;
            response.FileName = Path.Combine(response.Folder, fileName);
        }

        private void AppendEntities(LogController controller, CSharpRequest request, StringBuilder stringBuilder)
        {
            if (!request.Entities)
                return;
            var types = GetRecordTypesToImport(request);
            var countToDo = types.Count();
            var countDone = 0;
            stringBuilder.AppendLine(string.Format("\tpublic static class {0}", "Entities"));
            stringBuilder.AppendLine("\t{");
            foreach (var recordType in types.OrderBy(t => t))
            {
                controller.UpdateProgress(countDone, countToDo,
                    string.Format("Processing Entities ({0})", Service.GetDisplayName(recordType)));

                if (!string.IsNullOrWhiteSpace(recordType))
                    stringBuilder.AppendLine(string.Format("\t\tpublic const string {0} = \"{1}\";", recordType,
                        recordType));
                countDone++;
            }
            stringBuilder.AppendLine("\t}");
        }

        private void AppendRelationships(LogController controller, CSharpRequest request, StringBuilder stringBuilder)
        {
            if (!request.Relationships)
                return;
            var types = GetRecordTypesToImport(request);
            var countToDo = types.Count();
            var countDone = 0;
            var relationshipsAdded = false;
            countDone = 0;
            foreach (var recordType in types.OrderBy(t => t))
            {
                controller.UpdateProgress(countDone, countToDo,
                    string.Format("Processing Relationships ({0})", Service.GetDisplayName(recordType)));
                if (!string.IsNullOrWhiteSpace(recordType))
                {
                    var relationships = Service.GetManyToManyRelationships(recordType);
                    if (relationships.Any())
                    {
                        if (!relationshipsAdded)
                        {
                            stringBuilder.AppendLine(string.Format("\tpublic static class {0}", "Relationships"));
                            stringBuilder.AppendLine("\t{");
                            relationshipsAdded = true;
                        }
                        stringBuilder.AppendLine(string.Format("\t\tpublic static class {0}_", recordType));
                        stringBuilder.AppendLine("\t\t{");
                        foreach (var relationship in relationships.OrderBy(r => r.SchemaName))
                        {
                            stringBuilder.AppendLine(string.Format("\t\t\tpublic static class {0}", relationship.SchemaName));
                            stringBuilder.AppendLine("\t\t\t{");
                            stringBuilder.AppendLine(string.Format("\t\t\t\tpublic const string Name = \"{0}\";",
                                relationship.SchemaName));
                            stringBuilder.AppendLine(string.Format("\t\t\t\tpublic const string EntityName = \"{0}\";",
                                relationship.IntersectEntityName));
                            stringBuilder.AppendLine("\t\t\t}");
                        }
                        stringBuilder.AppendLine("\t\t}");
                    }
                }
                countDone++;
            }
            if (relationshipsAdded)
                stringBuilder.AppendLine("\t}");
        }

        private void AppendFields(LogController controller, StringBuilder stringBuilder, CSharpRequest request)
        {
            if (!request.Fields)
                return;
            var types = GetRecordTypesToImport(request);
            var countToDo = types.Count();
            stringBuilder.AppendLine("\tpublic static class Fields");
            stringBuilder.AppendLine("\t{");
            var countDone = 0;
            foreach (var recordType in types.OrderBy(t => t))
            {
                controller.UpdateProgress(countDone, countToDo,
                    string.Format("Processing Fields ({0})", Service.GetDisplayName(recordType)));
                if (!string.IsNullOrWhiteSpace(recordType))
                {
                    stringBuilder.AppendLine(string.Format("\t\tpublic static class {0}_", recordType));
                    stringBuilder.AppendLine("\t\t{");
                    var fieldLabels = new Dictionary<string, string>();
                    foreach (var field in Service.GetFields(recordType).OrderBy(f => f))
                    {
                        if (!string.IsNullOrWhiteSpace(field))
                            stringBuilder.AppendLine(string.Format("\t\t\tpublic const string {0} = \"{1}\";",
                                CreateCodeLabel(field), field));
                    }
                    stringBuilder.AppendLine("\t\t}");
                }
                countDone++;
            }
            stringBuilder.AppendLine("\t}");
        }

        private IEnumerable<string> GetRecordTypesToImport(CSharpRequest request)
        {
            return request.IncludeAllRecordTypes
                ? Service.GetAllRecordTypes()
                : request.RecordTypes.Select(r => r.RecordType.Key);
        }

        private void AppendActions(StringBuilder stringBuilder, CSharpRequest request,
            LogController controller)
        {
            if (!request.Actions)
                return;

            controller.UpdateProgress(1, 2,
                        string.Format("Processing Actions"));

            var actions = Service.RetrieveAllAndClauses("workflow", new[]
            {
                new Condition("category", ConditionType.Equal, XrmPicklists.WorkflowCategory.Action),
                new Condition("type", ConditionType.Equal, XrmPicklists.WorkflowType.Definition)
            }, null);

            if (actions.Any())
            {
                var requests = Service.RetrieveAllOrClauses(Entities.sdkmessage,
                    actions.Select(a => a.GetLookupId(Fields.workflow_.sdkmessageid))
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => new Condition(Fields.sdkmessage_.sdkmessageid, ConditionType.Equal, s))
                    , null);

                stringBuilder.AppendLine("\tpublic static class Actions");
                stringBuilder.AppendLine("\t{");
                foreach (var action in actions.OrderBy(a => a.GetStringField(Fields.workflow_.name)))
                {
                    var noTarget = action.GetStringField("primaryentity") == "none";

                    var sdkMessageId = action.GetLookupId(Fields.workflow_.sdkmessageid);
                    var matchingRequestNames = requests
                        .Where(r => r.Id == sdkMessageId)
                        .Select(r => r.GetStringField(Fields.workflow_.name))
                        .ToArray();
                    if (matchingRequestNames.Any())
                    {
                        var actionName = matchingRequestNames.First();
                        var inArguments = new List<string>();
                        var outArguments = new List<string>();

                        var document = new XmlDocument();
                        document.LoadXml(action.GetStringField("xaml"));


                        foreach (XmlNode childNode in document.ChildNodes)
                        {
                            if (childNode.Name == "Activity")
                            {
                                foreach (XmlNode childNode2 in childNode.ChildNodes)
                                {
                                    if (childNode2.Name == "x:Members")
                                    {
                                        foreach (XmlNode property in childNode2.ChildNodes)
                                        {
                                            foreach (XmlNode propertyChild in property.ChildNodes)
                                            {
                                                if (propertyChild.Name == "x:Property.Attributes")
                                                {
                                                    var argumentName = property.Attributes != null
                                                                       && property.Attributes["Name"] != null
                                                        ? property.Attributes["Name"].InnerText
                                                        : null;
                                                    foreach (XmlNode attribute in propertyChild.ChildNodes)
                                                    {
                                                        var direction = attribute.Attributes != null
                                                                        && attribute.Attributes["Value"] != null
                                                            ? attribute.Attributes["Value"].InnerText
                                                            : null;
                                                        if (direction == "Input")
                                                        {
                                                            //don't add a=target of global action type
                                                            if (!noTarget || argumentName != "Target")
                                                                inArguments.Add(argumentName);
                                                        }
                                                        if (direction == "Output")
                                                        {
                                                            outArguments.Add(argumentName);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        stringBuilder.AppendLine(string.Format("\t\tpublic static class {0}", actionName));
                        stringBuilder.AppendLine("\t\t{");
                        stringBuilder.AppendLine(string.Format("\t\t\tpublic const string Name = \"{0}\";", actionName));
                        if (inArguments.Any())
                        {
                            stringBuilder.AppendLine("\t\t\tpublic static class In");
                            stringBuilder.AppendLine("\t\t\t{");
                            foreach (var item in inArguments)
                            {
                                stringBuilder.AppendLine(string.Format("\t\t\t\tpublic const string {0} = \"{0}\";", item));
                            }
                            stringBuilder.AppendLine("\t\t\t}");
                        }
                        if (outArguments.Any())
                        {
                            stringBuilder.AppendLine("\t\t\tpublic static class Out");
                            stringBuilder.AppendLine("\t\t\t{");
                            foreach (var item in outArguments)
                            {
                                stringBuilder.AppendLine(string.Format("\t\t\t\tpublic const string {0} = \"{0}\";", item));
                            }
                            stringBuilder.AppendLine("\t\t\t}");

                        }
                        stringBuilder.AppendLine("\t\t}");
                    }
                }
                stringBuilder.AppendLine("\t}");
            }
        }

        private void AppendOptionSets(StringBuilder stringBuilder, CSharpRequest request,
            LogController controller)
        {
            if (!request.FieldOptions && !request.SharedOptions)
                return;
            stringBuilder.AppendLine("\tpublic static class OptionSets");
            stringBuilder.AppendLine("\t{");

            if (request.SharedOptions)
            {
                var picklists = Service.GetSharedPicklists();
                var countOptionsToDo = picklists.Count();
                var countOptionsDone = 0;

                var duplicateSharedLabels = picklists
                    .GroupBy(t => CreateCodeLabel(t.DisplayName), t => t)
                    .Where(t => t.Count() > 1)
                    .Select(t => t.Key)
                    .ToArray();

                stringBuilder.AppendLine("\t\tpublic static class Shared");
                stringBuilder.AppendLine("\t\t{");
                foreach (var item in picklists.OrderBy(s => CreateCodeLabel(string.IsNullOrWhiteSpace(s.DisplayName) ? s.SchemaName : s.DisplayName)))
                {
                    controller.UpdateProgress(countOptionsDone, countOptionsToDo,
                        string.Format("Processing Shared Options ({0})", item.DisplayName));
                    var optionSetLabel = CreateCodeLabel(string.IsNullOrWhiteSpace(item.DisplayName) ? item.SchemaName : item.DisplayName);
                    if (duplicateSharedLabels.Contains(optionSetLabel))
                        optionSetLabel = optionSetLabel + "_" + item.SchemaName;
                    stringBuilder.AppendLine("\t\t\tpublic static class " + optionSetLabel);
                    stringBuilder.AppendLine("\t\t\t{");
                    var labelCounts = item.PicklistOptions
                        .Select(o => CreateCodeLabel(o.Value))
                        .GroupBy(s => s);

                    foreach (var option in item.PicklistOptions)
                    {
                        if (IsValidForCode(option))
                        {
                            var optionLabel = CreateCodeLabel(option.Value);
                            var isDuplicateLabel = labelCounts.First(g => g.Key == optionLabel).Count() > 1;

                            if (isDuplicateLabel || optionLabel == optionSetLabel)
                                optionLabel = optionLabel + "_" + option.Key;

                            stringBuilder.AppendLine(
                            string.Format("\t\t\t\tpublic const int {0} = {1};", optionLabel, option.Key));
                        }
                    }
                    stringBuilder.AppendLine("\t\t\t}");
                }
                stringBuilder.AppendLine("\t\t}");
            }
            if (request.FieldOptions)
            {
                var types = GetRecordTypesToImport(request);
                var countToDo = types.Count();
                var countDone = 0;
                var duplicateTypesLabels = types
                    .GroupBy(t => CreateCodeLabel(Service.GetDisplayName(t)), t => t)
                    .Where(t => t.Count() > 1)
                    .Select(t => t.Key)
                    .ToArray();
                foreach (var recordType in types.OrderBy(r => r))
                {
                    controller.UpdateProgress(countDone, countToDo,
                        string.Format("Processing Options ({0})", Service.GetDisplayName(recordType)));

                    if (IsValidForCode(recordType))
                    {
                        var recordTypeCodeLabel = CreateCodeLabel(Service.GetDisplayName(recordType));
                        if (duplicateTypesLabels.Contains(recordTypeCodeLabel))
                            recordTypeCodeLabel = recordTypeCodeLabel + "_" + recordType;
                        stringBuilder.AppendLine("\t\tpublic static class " + recordTypeCodeLabel);
                        stringBuilder.AppendLine("\t\t{");
                        var optionFields = Service.GetFields(recordType).Where(f => IsValidForOptionSetCode(f, recordType));
                        var optionFieldLabels = optionFields.ToDictionary(f => f,
                            f => CreateCodeLabel(Service.GetFieldLabel(f, recordType)));
                        foreach (var field in optionFieldLabels.OrderBy(f => f.Value))
                        {
                            var fieldLabel = field.Value;
                            if (optionFieldLabels.Any(kv => kv.Key != field.Key && kv.Value == field.Value))
                                fieldLabel = string.Format("{0}_{1}", fieldLabel, field.Key);
                            if (fieldLabel == recordTypeCodeLabel)
                                fieldLabel = string.Format("{0}_", fieldLabel);
                            stringBuilder.AppendLine("\t\t\tpublic static class " + fieldLabel);
                            stringBuilder.AppendLine("\t\t\t{");
                            var options = Service.GetPicklistKeyValues(field.Key, recordType);

                            var labelCounts = options
                                .Select(o => CreateCodeLabel(o.Value))
                                .GroupBy(s => s);
                            foreach (var option in options)
                            {
                                if (IsValidForCode(option))
                                {
                                    var optionLabel = CreateCodeLabel(option.Value);
                                    var isDuplicateLabel = labelCounts.First(g => g.Key == optionLabel).Count() > 1;

                                    if (optionLabel == fieldLabel)
                                        optionLabel = string.Format("{0}_", optionLabel);
                                    if (isDuplicateLabel)
                                        optionLabel = optionLabel + "_" + option.Key;

                                    stringBuilder.AppendLine(
                                        string.Format("\t\t\t\tpublic const int {0} = {1};", optionLabel, option.Key));
                                }
                            }
                            stringBuilder.AppendLine("\t\t\t}");

                        }
                        stringBuilder.AppendLine("\t\t}");
                    }
                    countDone++;
                }
            }
            stringBuilder.AppendLine("\t}");
        }
    }
}