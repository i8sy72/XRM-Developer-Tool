﻿using JosephM.Core.Attributes;
using JosephM.Core.Service;
using System.Collections;
using System.Collections.Generic;

namespace JosephM.Prism.TestModule.Prism.TestDialog
{
    public class TestDialogResponse : ServiceResponseBase<TestDialogResponseItem>
    {
        public string StringProperty { get { return "String Property Value"; } }

        public int IntProperty { get { return 50; } }

        public bool BoolProperty { get { return true; } }

        [AllowDownload]
        public IEnumerable<SomeOtherType> SomethingElseToDisplay
        {
            get
            {
                var listOfThings = new List<SomeOtherType>();
                for(var i = 0; i < 75; i++)
                {
                    listOfThings.Add(new SomeOtherType());
                }
                return listOfThings;
            }
        }
    }

    public class SomeOtherType
    {
        public string StringProperty { get { return "String Property Value"; } }

        public int IntProperty { get { return 50; } }

    }
}