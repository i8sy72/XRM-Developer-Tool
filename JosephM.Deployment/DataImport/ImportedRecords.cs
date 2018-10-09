﻿using JosephM.Core.Attributes;

namespace JosephM.Deployment.DataImport
{
    [DoNotAllowGridOpen]
    public class ImportedRecords
    {
        [DisplayOrder(10)]
        public string Type { get; set; }
        [DisplayOrder(20)]
        [GridWidth(125)]
        public int Created { get; set; }
        [DisplayOrder(30)]
        [GridWidth(125)]
        public int Updated { get; set; }
        [DisplayOrder(40)]
        [GridWidth(125)]
        public int NoChange { get; set; }
        [DisplayOrder(50)]
        [GridWidth(125)]
        public int Errors { get; set; }
    }
}