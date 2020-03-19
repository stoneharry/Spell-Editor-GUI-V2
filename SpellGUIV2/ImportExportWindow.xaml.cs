﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SpellEditor.Sources.Binding;
using SpellEditor.Sources.Config;
using SpellEditor.Sources.Database;
using StormLibSharp;

namespace SpellEditor
{
    partial class ImportExportWindow
    {
        private readonly IDatabaseAdapter _Adapter;
        public List<string> BindingImportList = new List<string>();
        public List<string> BindingExportList = new List<string>();

        public bool IsDataSelected() => BindingImportList.Count > 0 || BindingExportList.Count > 0;

        public ImportExportWindow(IDatabaseAdapter adapter)
        {
            _Adapter = adapter;
            InitializeComponent();
        }

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Console.WriteLine("ERROR: " + e.Exception.Message);
            File.WriteAllText("error.txt", e.Exception.Message, Encoding.GetEncoding(0));
            e.Handled = true;
        }

        private void _Loaded(object sender, RoutedEventArgs e)
        {
            Application.Current.DispatcherUnhandledException += App_DispatcherUnhandledException;
            BuildImportTab();
            BuildExportTab();
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //var item = sender as TabControl;
        }

        private void BuildImportTab()
        {
            var contents = ImportGrid.Children;
            if (contents.Count > 0)
                return;
            contents.Add(new Label
            {
                Content = "The Spell DBC is the file that needs to be imported for this program to work."
            });
            var importBtn = new Button
            {
                Content = "Import Checked DBC Files",
                Padding = new Thickness(2, 2, 2, 2)
            };
            importBtn.Click += ImportClick;
            contents.Add(importBtn);
            foreach (var binding in BindingManager.GetInstance().GetAllBindings())
            {
                var numRows = binding.GetNumRowsInTable(_Adapter);
                contents.Add(new CheckBox
                {
                    Name = binding.Name + "ImportCheckBox",
                    Content = $"Import {binding.Name}.dbc {(numRows > 0 ? $"- {numRows} rows" : "")}",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsEnabled = numRows == 0,
                    IsChecked = numRows == 0 && 
                        (binding.Name.Equals("Spell") || 
                        binding.Name.Contains("SpellVisual"))
                });
            }
        }

        private void BuildExportTab()
        {
            var contents = ExportGrid.Children;
            if (contents.Count > 0)
                return;
            contents.Add(new Label
            {
                Content = "Select which imported tables you wish to export to new DBC files."
            });
            var exportBtn = new Button
            {
                Content = "Export Checked DBC Files",
                Padding = new Thickness(4, 5, 4, 5)
            };
            var mpqBtn = new Button
            {
                Content = "Create MPQ from DBC files",
                Padding = new Thickness(4, 5, 4, 5)
            };
            mpqBtn.Click += MpqClick;
            exportBtn.Click += ExportClick;
            contents.Add(exportBtn);
            contents.Add(mpqBtn);
            foreach (var binding in BindingManager.GetInstance().GetAllBindings())
            {
                var numRows = binding.GetNumRowsInTable(_Adapter);
                contents.Add(new CheckBox
                {
                    Name = binding.Name + "ExportCheckBox",
                    Content = $"Export {(numRows > 0 ? numRows.ToString() : "")} {binding.Name} {(numRows > 0 ? "rows " : "")}to Export\\{binding.Name}.dbc",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsEnabled = numRows > 0,
                    IsChecked = numRows > 0 && 
                        (binding.Name.Equals("Spell") ||
                        binding.Name.Contains("SpellVisual"))
                });
            }
        }

        private void MpqClick(object sender, RoutedEventArgs e)
        {
            // handle exceptions FIXME
            var archivePath = "Export\\patch-4.mpq";
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
            using (var archive = MpqArchive.CreateNew(archivePath, MpqArchiveVersion.Version1))
            {
                foreach (var dbcFile in Directory.EnumerateFiles("Export"))
                {
                    if (dbcFile.EndsWith(".dbc"))
                    {
                        archive.AddFileFromDisk(dbcFile, "DBFileClient\\" + dbcFile.Substring(dbcFile.IndexOf('\\') + 1));
                    }
                }
            }
        }

        private void ImportClick(object sender, RoutedEventArgs e) => ClickHandler(true);
        private void ExportClick(object sender, RoutedEventArgs e) => ClickHandler(false);
        private void ClickHandler(bool isImport)
        {
            var bindingNameList = new List<string>();
            var children = isImport ? ImportGrid.Children : ExportGrid.Children;
            var prefix = isImport ? "Import" : "Export";
            foreach (var element in children)
            {
                if (element is CheckBox box)
                {
                    if (box.IsChecked.HasValue && box.IsChecked.Value)
                        bindingNameList.Add(box.Name.Substring(0, box.Name.IndexOf(prefix + "CheckBox")));
                }
            }
            if (isImport)
                BindingImportList = bindingNameList;
            else
                BindingExportList = bindingNameList;
            Console.WriteLine($"Bindings selected to {prefix.ToLower()}: {String.Join(", ", bindingNameList)}");
        }
    }
}
