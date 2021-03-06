﻿using System.Windows;
using System.Windows.Controls;
using TerrainEditor.Core.Services;
using TerrainEditor.ViewModels;

namespace TerrainEditor.UserControls
{

    public partial class UvMappingPropertyEditor : UserControl
    {
        private SelectMappingDialog m_mappingDialog;

        public UvMappingPropertyEditor()
        {
            InitializeComponent();
        }
        private void SelectNewMapping(object sender, RoutedEventArgs e)
        {
            m_mappingDialog = new SelectMappingDialog();
            m_mappingDialog.ClosingFinished += MappingDialogOnClosingFinished;

            ServiceLocator
                .Get<IDialogBoxService>()
                .ShowCustomDialog(m_mappingDialog);
        }
        private void MappingDialogOnClosingFinished(object sender, RoutedEventArgs routedEventArgs)
        {
            SetValue(DataContextProperty, m_mappingDialog.SelectedMapping ?? DataContext);

            m_mappingDialog.ClosingFinished -= MappingDialogOnClosingFinished;
            m_mappingDialog = null;
        }
        private void OnDropMapping(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(UvMapping)))
                SetValue(DataContextProperty, e.Data.GetData(typeof(UvMapping)));
        }
    }
}
