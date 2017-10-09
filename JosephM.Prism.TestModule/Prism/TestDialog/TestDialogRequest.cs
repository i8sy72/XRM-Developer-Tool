﻿using System.Collections;
using System.Collections.Generic;
using JosephM.Core.Attributes;
using JosephM.Core.Service;
using JosephM.Application.ViewModel.SettingTypes;
using JosephM.Core.FieldType;
using JosephM.Application.ViewModel.Attributes;
using JosephM.Prism.TestModule.Prism.TestSettings;

namespace JosephM.Prism.TestModule.Prism.TestDialog
{
    [DisplayName("Test Dialog")]
    public class TestDialogRequest : ServiceRequestBase
    {
        public TestDialogRequest()
        {
            Items = new[] {new TestDialogRequestItem()};
        }
        public bool ThrowResponseErrors { get; set; }

        [SettingsLookup(typeof(ITestSettings), nameof(ITestSettings.Settings))]
        public TestSetting TestSetting { get; set; }

        [RecordTypeFor(nameof(Fields) + "." + nameof(FieldSetting.RecordField))]
        [RecordTypeFor(nameof(SpecificRecordsToExport) + "." + nameof(LookupSetting.Record))]
        public RecordType RecordType { get; set; }

        [PropertyInContextByPropertyNotNull(nameof(RecordType))]
        public IEnumerable<FieldSetting> Fields { get; set; }

        [PropertyInContextByPropertyNotNull(nameof(RecordType))]
        public IEnumerable<LookupSetting> SpecificRecordsToExport { get; set; }

        public IEnumerable<RecordTypeSetting> RecordTypes { get; set; }



        [DoNotAllowAdd]
        public IEnumerable<TestDialogRequestItem> Items { get; set; }



        public class TestDialogRequestItem
        {
            public TestDialogRequestItem()
            {
                ReadOnlyProp = "I Read Only";
            }

            [ReadOnlyWhenSet]
            public string ReadOnlyProp { get; set; }
        }
    }
} ;