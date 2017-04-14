﻿#region

using System;
using System.Windows;
using System.Windows.Controls;
using JosephM.Application.ViewModel.RecordEntry.Section;

#endregion

namespace JosephM.Wpf.TemplateSelector
{
    public class FormSectionTemplateSelector : DataTemplateSelector
    {
        public DataTemplate FieldSectionTemplate { get; set; }
        public DataTemplate GridSectionTemplate { get; set; }
        public DataTemplate FieldSectionCompactTemplate { get; set; }
        
        public override DataTemplate SelectTemplate(object item,
            DependencyObject container)
        {
            if (item is FieldSectionViewModel)
            {
                var vm = (FieldSectionViewModel)item;
                if (vm.WrapHorizontal)
                    return FieldSectionCompactTemplate;
                else
                    return FieldSectionTemplate;
            }
            if (item is GridSectionViewModel)
                return GridSectionTemplate;
            throw new ArgumentOutOfRangeException(string.Concat("No template defined for the type",
                item == null ? "null" : item.GetType().FullName));
        }
    }
}