using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MySql.Data.MySqlClient;
using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using SpellEditor.Sources.Binding;
using SpellEditor.Sources.BLP;
using SpellEditor.Sources.Config;
using SpellEditor.Sources.Constants;
using SpellEditor.Sources.Controls;
using SpellEditor.Sources.Controls.Visual;
using SpellEditor.Sources.Database;
using SpellEditor.Sources.DBC;
using SpellEditor.Sources.SpellStringTools;
using SpellEditor.Sources.Tools.MPQ;
using SpellEditor.Sources.Tools.SpellFamilyClassMaskStoreParser;
using SpellEditor.Sources.Tools.VisualTools;
using SpellEditor.Sources.VersionControl;

namespace SpellEditor
{
    partial class MainWindow
    {
        #region Boxes
        private readonly Dictionary<int, ThreadSafeTextBox> stringObjectMap = new Dictionary<int, ThreadSafeTextBox>();
        private readonly List<ThreadSafeCheckBox> attributes0 = new List<ThreadSafeCheckBox>();
        private readonly List<ThreadSafeCheckBox> attributes1 = new List<ThreadSafeCheckBox>();
        private readonly List<ThreadSafeCheckBox> attributes2 = new List<ThreadSafeCheckBox>();
        private readonly List<ThreadSafeCheckBox> attributes3 = new List<ThreadSafeCheckBox>();
        private readonly List<ThreadSafeCheckBox> attributes4 = new List<ThreadSafeCheckBox>();
        private readonly List<ThreadSafeCheckBox> attributes5 = new List<ThreadSafeCheckBox>();
        private readonly List<ThreadSafeCheckBox> attributes6 = new List<ThreadSafeCheckBox>();
        private readonly List<ThreadSafeCheckBox> attributes7 = new List<ThreadSafeCheckBox>();
        private readonly List<ThreadSafeCheckBox> stancesBoxes = new List<ThreadSafeCheckBox>();
        private readonly List<ThreadSafeCheckBox> targetCreatureTypeBoxes = new List<ThreadSafeCheckBox>();
        private readonly List<ThreadSafeCheckBox> targetBoxes = new List<ThreadSafeCheckBox>();
        private readonly List<ThreadSafeCheckBox> procBoxes = new List<ThreadSafeCheckBox>();
        private readonly List<ThreadSafeCheckBox> interrupts1 = new List<ThreadSafeCheckBox>();
        private readonly List<ThreadSafeCheckBox> interrupts2 = new List<ThreadSafeCheckBox>();
        private readonly List<ThreadSafeCheckBox> interrupts3 = new List<ThreadSafeCheckBox>();
        public readonly List<ThreadSafeCheckBox> equippedItemInventoryTypeMaskBoxes = new List<ThreadSafeCheckBox>();
        public readonly List<ThreadSafeCheckBox> equippedItemSubClassMaskBoxes = new List<ThreadSafeCheckBox>();
        #endregion

        #region MemberVariables
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private IDatabaseAdapter adapter;
        public uint selectedID;
        public uint newIconID = 1;
        private bool updating;
        private readonly DataTable spellTable = new DataTable();
        private int storedLocale = -1;
        private readonly SpellStringParser SpellStringParser = new SpellStringParser();

        private readonly List<ThreadSafeTextBox> spellDescGenFields = new List<ThreadSafeTextBox>();
        private readonly List<ThreadSafeTextBox> spellTooltipGenFields = new List<ThreadSafeTextBox>();
        public SpellFamilyClassMaskParser spellFamilyClassMaskParser;
        private VisualController _currentVisualController;
        #endregion

        public IDatabaseAdapter GetDBAdapter()
        {
            return adapter;
        }

        public MainWindow()
        {
            // Ensure the decimal seperator used is always a full stop
            var customCulture = (CultureInfo)Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            Thread.CurrentThread.CurrentCulture = customCulture;

            // Setup logging
            var config = new LoggingConfiguration();
            var logfile = new FileTarget("logfile") { FileName = "debug_output.txt" };
            var logconsole = new ConsoleTarget("logconsole");
            var layout = Layout.FromString("[${time}|${level:uppercase=true}|${logger}] ${message}");
            logfile.Layout = layout;
            logconsole.Layout = layout;
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile);
            LogManager.Configuration = config;

            // Banner
            Logger.Info("######################################################");
            Logger.Info($"Starting WoW Spell Editor - {DateTime.Now.ToString()}");
            Logger.Info("######################################################");
            
            // Config must be initialised fast
            Config.Init();
            InitializeComponent();
        }

        public async void HandleErrorMessage(string msg)
        {
            if (Dispatcher != null && Dispatcher.CheckAccess())
                await this.ShowMessageAsync(SafeTryFindResource("SpellEditor"), msg);
            else
                Dispatcher?.Invoke(DispatcherPriority.Normal, TimeSpan.Zero, new Func<object>(() => this.ShowMessageAsync(SafeTryFindResource("SpellEditor"), msg)));
        }

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Info("ERROR: " + e.Exception + "\n" + e.Exception.InnerException);
            File.WriteAllText("error.txt", e.Exception + "\n" + e.Exception.InnerException, Encoding.GetEncoding(0));
            HandleErrorMessage(e.Exception + "\n\n" + e.Exception.InnerException);
            e.Handled = true;
            Console.Out.Flush();
        }

        public int GetLanguage() {
            // FIXME(Harry)
            // Disabled returning Locale_langauge until it can at least support multiple client types
            return GetLocale() == -1 ? 0 : GetLocale();
            //return (int)Locale_language;
        }

        #region LanguageSwitch
        private void RefreshAllUIElements()
        {
            Attributes1.Children.Clear();
            attributes0.Clear();
            string[] attFlags = SafeTryFindResource("attFlags_strings").Split('|');
            foreach (string attFlag in attFlags)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = attFlag, ToolTip = attFlag, Margin = new Thickness(0, 5, 0, 0)
                };

                Attributes1.Children.Add(box);
                attributes0.Add(box);
            }

            Attributes2.Children.Clear();
            attributes1.Clear();
            attFlags = SafeTryFindResource("attFlagsEx_strings").Split('|');
            foreach (string attFlag in attFlags)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = attFlag, ToolTip = attFlag, Margin = new Thickness(0, 5, 0, 0)
                };

                Attributes2.Children.Add(box);
                attributes1.Add(box);
            }

            Attributes3.Children.Clear();
            attributes2.Clear();
            attFlags = SafeTryFindResource("attFlagsEx1_strings").Split('|');
            foreach (string attFlag in attFlags)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = attFlag, ToolTip = attFlag, Margin = new Thickness(0, 5, 0, 0)
                };

                Attributes3.Children.Add(box);
                attributes2.Add(box);
            }

            Attributes4.Children.Clear();
            attributes3.Clear();
            attFlags = SafeTryFindResource("attFlagsEx2_strings").Split('|');
            foreach (string attFlag in attFlags)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = attFlag, ToolTip = attFlag, Margin = new Thickness(0, 5, 0, 0)
                };

                Attributes4.Children.Add(box);
                attributes3.Add(box);
            }

            Attributes5.Children.Clear();
            attributes4.Clear();
            attFlags = SafeTryFindResource("attFlagsEx3_strings").Split('|');
            foreach (string attFlag in attFlags)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = attFlag, ToolTip = attFlag, Margin = new Thickness(0, 5, 0, 0)
                };

                Attributes5.Children.Add(box);
                attributes4.Add(box);
            }

            Attributes6.Children.Clear();
            attributes5.Clear();
            attFlags = SafeTryFindResource("attFlagsEx4_strings").Split('|');
            foreach (string attFlag in attFlags)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = attFlag, ToolTip = attFlag, Margin = new Thickness(0, 5, 0, 0)
                };

                Attributes6.Children.Add(box);
                attributes5.Add(box);
            }

            Attributes7.Children.Clear();
            attributes6.Clear();
            attFlags = SafeTryFindResource("attFlagsEx5_strings").Split('|');
            foreach (string attFlag in attFlags)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = attFlag, ToolTip = attFlag, Margin = new Thickness(0, 5, 0, 0)
                };

                Attributes7.Children.Add(box);
                attributes6.Add(box);
            }

            Attributes8.Children.Clear();
            attributes7.Clear();
            attFlags = SafeTryFindResource("attFlagsEx6_strings").Split('|');
            foreach (string attFlag in attFlags)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = attFlag, ToolTip = attFlag, Margin = new Thickness(0, 5, 0, 0)
                };

                Attributes8.Children.Add(box);
                attributes7.Add(box);
            }

            StancesGrid.Children.Clear();
            stancesBoxes.Clear();
            string[] stances_strings = SafeTryFindResource("stances_strings").Split('|');
            foreach (string stance in stances_strings)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = stance, ToolTip = stance, Margin = new Thickness(0, 5, 0, 0)
                };


                StancesGrid.Children.Add(box);
                stancesBoxes.Add(box);
            }

            TargetCreatureType.Children.Clear();
            targetCreatureTypeBoxes.Clear();
            string[] creature_type_strings = SafeTryFindResource("creature_type_strings").Split('|');
            foreach (string creatureType in creature_type_strings)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = creatureType, ToolTip = creatureType, Margin = new Thickness(0, 5, 0, 0)
                };


                TargetCreatureType.Children.Add(box);
                targetCreatureTypeBoxes.Add(box);
            }

            CasterAuraState.Items.Clear();
            string[] caster_aura_state_strings = SafeTryFindResource("caster_aura_state_strings").Split('|');
            foreach (string casterAuraState in caster_aura_state_strings) { CasterAuraState.Items.Add(casterAuraState); }

            TargetAuraState.Items.Clear();
            string[] target_aura_state_strings = SafeTryFindResource("target_aura_state_strings").Split('|');
            foreach (string targetAuraState in target_aura_state_strings) { TargetAuraState.Items.Add(targetAuraState); }

            EquippedItemInventoryTypeGrid.Children.Clear();
            equippedItemInventoryTypeMaskBoxes.Clear();
            string[] equipped_item_inventory_type_mask_strings = SafeTryFindResource("equipped_item_inventory_type_mask_strings").Split('|');
            foreach (string equippedItemInventoryTypeMask in equipped_item_inventory_type_mask_strings)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = equippedItemInventoryTypeMask, Margin = new Thickness(0, 5, 0, 0)
                };


                EquippedItemInventoryTypeGrid.Children.Add(box);
                equippedItemInventoryTypeMaskBoxes.Add(box);
            }

            EquippedItemSubClassGrid.Children.Clear();
            equippedItemSubClassMaskBoxes.Clear();
            for (int i = 0; i < 29; ++i)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = SafeTryFindResource("None"),
                    Margin = new Thickness(0, 5, 0, 0),
                    Visibility = Visibility.Hidden
                };

                EquippedItemSubClassGrid.Children.Add(box);
                equippedItemSubClassMaskBoxes.Add(box);
            }

            PowerType.Items.Clear();
            string[] school_strings = SafeTryFindResource("school_strings").Split('|');
            foreach (string schoolString in school_strings) { PowerType.Items.Add(schoolString); }

            SpellDamageType.Items.Clear();
            PreventionType.Items.Clear();
            string[] damage_prevention_types = SafeTryFindResource("damage_prevention_types").Split('|');
            for (int i = 0; i < damage_prevention_types.Length; ++i)
            {
                if (i < 4)
                {
                    SpellDamageType.Items.Add(damage_prevention_types[i]);
                }
                else
                {
                    PreventionType.Items.Add(damage_prevention_types[i]);
                }
            }

            TargetEditorGrid.Children.Clear();
            targetBoxes.Clear();
            string[] target_strings = SafeTryFindResource("target_strings").Split('|');
            foreach (string targetString in target_strings)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = targetString, Margin = new Thickness(0, 5, 0, 0)
                };


                TargetEditorGrid.Children.Add(box);
                targetBoxes.Add(box);
            }

            ProcEditorGrid.Children.Clear();
            procBoxes.Clear();
            string[] proc_strings = SafeTryFindResource("proc_strings").Split('|');
            foreach (string procString in proc_strings)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = procString, Margin = new Thickness(0, 5, 0, 0)
                };



                ProcEditorGrid.Children.Add(box);
                procBoxes.Add(box);
            }

            ApplyAuraName1.Items.Clear();
            ApplyAuraName2.Items.Clear();
            ApplyAuraName3.Items.Clear();
            FilterAuraCombo.Items.Clear();
            string[] spell_aura_effect_names = SafeTryFindResource("spell_aura_effect_names").Split('|');
            for (int i = 0; i < spell_aura_effect_names.Length; ++i)
            {
                var auraName = i + " - " + spell_aura_effect_names[i];
                ApplyAuraName1.Items.Add(auraName);
                ApplyAuraName2.Items.Add(auraName);
                ApplyAuraName3.Items.Add(auraName);
                FilterAuraCombo.Items.Add(auraName);
            }

            SpellEffect1.Items.Clear();
            SpellEffect2.Items.Clear();
            SpellEffect3.Items.Clear();
            FilterSpellEffectCombo.Items.Clear();
            string[] spell_effect_names = SafeTryFindResource("spell_effect_names").Split('|');
            for (int i = 0; i < spell_effect_names.Length; ++i)
            {
                var effectName = i + " - " + spell_effect_names[i];
                SpellEffect1.Items.Add(effectName);
                SpellEffect2.Items.Add(effectName);
                SpellEffect3.Items.Add(effectName);
                FilterSpellEffectCombo.Items.Add(effectName);
            }

            Mechanic1.Items.Clear();
            Mechanic2.Items.Clear();
            Mechanic3.Items.Clear();
            string[] mechanic_names = SafeTryFindResource("mechanic_names").Split('|');
            foreach (string mechanicName in mechanic_names)
            {
                Mechanic1.Items.Add(mechanicName);
                Mechanic2.Items.Add(mechanicName);
                Mechanic3.Items.Add(mechanicName);
            }

            if (TargetA1.Items.Count == 0)
            {
                int number = 0;
                foreach (Targets t in Enum.GetValues(typeof(Targets)))
                {
                    string toDisplay = number + " - " + t;
                    TargetA1.Items.Add(toDisplay);
                    TargetB1.Items.Add(toDisplay);
                    TargetA2.Items.Add(toDisplay);
                    TargetB2.Items.Add(toDisplay);
                    TargetA3.Items.Add(toDisplay);
                    TargetB3.Items.Add(toDisplay);

                    //ChainTarget1.Items.Add(toDisplay);
                    //ChainTarget2.Items.Add(toDisplay);
                    //ChainTarget3.Items.Add(toDisplay);
                    ++number;
                }
            }

            InterruptFlagsGrid.Children.Clear();
            interrupts1.Clear();
            string[] interrupt_strings = SafeTryFindResource("interrupt_strings").Split('|');
            foreach (string interruptString in interrupt_strings)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = interruptString, Margin = new Thickness(0, 5, 0, 0)
                };

                InterruptFlagsGrid.Children.Add(box);
                interrupts1.Add(box);
            }

            AuraInterruptFlagsGrid.Children.Clear();
            interrupts2.Clear();
            string[] aura_interrupt_strings = SafeTryFindResource("aura_interrupt_strings").Split('|');
            foreach (string auraInterruptString in aura_interrupt_strings)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = auraInterruptString, Margin = new Thickness(0, 5, 0, 0)
                };

                AuraInterruptFlagsGrid.Children.Add(box);
                interrupts2.Add(box);
            }

            ChannelInterruptFlagsGrid.Children.Clear();
            interrupts3.Clear();
            string[] channel_interrupt_strings = SafeTryFindResource("channel_interrupt_strings").Split('|');
            foreach (string channelInterruptString in channel_interrupt_strings)
            {
                ThreadSafeCheckBox box = new ThreadSafeCheckBox
                {
                    Content = channelInterruptString, Margin = new Thickness(0, 5, 0, 0)
                };

                ChannelInterruptFlagsGrid.Children.Add(box);
                interrupts3.Add(box);
            }

            if (WoWVersionManager.IsTbcOrGreaterSelected)
            {
                S1.Content = SafeTryFindResource("checkboxPhysical");
                S2.Content = SafeTryFindResource("checkboxHoly");
                S3.Content = SafeTryFindResource("checkboxFire");
                S4.Content = SafeTryFindResource("checkboxNature");
                S5.Content = SafeTryFindResource("checkboxFrost");
                S6.Content = SafeTryFindResource("checkboxShadow");
                S7.Content = SafeTryFindResource("checkboxArcane");
            }
            else
            {
                S1.Content = SafeTryFindResource("checkboxHoly");
                S2.Content = SafeTryFindResource("checkboxFire");
                S3.Content = SafeTryFindResource("checkboxNature");
                S4.Content = SafeTryFindResource("checkboxFrost");
                S5.Content = SafeTryFindResource("checkboxShadow");
                S6.Content = SafeTryFindResource("checkboxArcane");
                S7.Content = "Unused";
            }
            S7.IsEnabled = WoWVersionManager.IsTbcOrGreaterSelected;
        }
        #endregion

        #region Loaded
        private void _Loaded(object sender, RoutedEventArgs e)
        {
            Application.Current.DispatcherUnhandledException += App_DispatcherUnhandledException;

            try
            {
                Title = $"{Title} - {WoWVersionManager.GetInstance().SelectedVersion().Version}";

                stringObjectMap.Add(0, SpellName0);
                stringObjectMap.Add(1, SpellName1);
                stringObjectMap.Add(2, SpellName2);
                stringObjectMap.Add(3, SpellName3);
                stringObjectMap.Add(4, SpellName4);
                stringObjectMap.Add(5, SpellName5);
                stringObjectMap.Add(6, SpellName6);
                stringObjectMap.Add(7, SpellName7);
                stringObjectMap.Add(8, SpellName8);
                stringObjectMap.Add(9, SpellRank0);
                stringObjectMap.Add(10, SpellRank1);
                stringObjectMap.Add(11, SpellRank2);
                stringObjectMap.Add(12, SpellRank3);
                stringObjectMap.Add(13, SpellRank4);
                stringObjectMap.Add(14, SpellRank5);
                stringObjectMap.Add(15, SpellRank6);
                stringObjectMap.Add(16, SpellRank7);
                stringObjectMap.Add(17, SpellRank8);
                stringObjectMap.Add(18, SpellTooltip0);
                stringObjectMap.Add(19, SpellTooltip1);
                stringObjectMap.Add(20, SpellTooltip2);
                stringObjectMap.Add(21, SpellTooltip3);
                stringObjectMap.Add(22, SpellTooltip4);
                stringObjectMap.Add(23, SpellTooltip5);
                stringObjectMap.Add(24, SpellTooltip6);
                stringObjectMap.Add(25, SpellTooltip7);
                stringObjectMap.Add(26, SpellTooltip8);
                stringObjectMap.Add(27, SpellDescription0);
                stringObjectMap.Add(28, SpellDescription1);
                stringObjectMap.Add(29, SpellDescription2);
                stringObjectMap.Add(30, SpellDescription3);
                stringObjectMap.Add(31, SpellDescription4);
                stringObjectMap.Add(32, SpellDescription5);
                stringObjectMap.Add(33, SpellDescription6);
                stringObjectMap.Add(34, SpellDescription7);
                stringObjectMap.Add(35, SpellDescription8);

                spellDescGenFields.Add(SpellDescriptionGen0);
                spellDescGenFields.Add(SpellDescriptionGen1);
                spellDescGenFields.Add(SpellDescriptionGen2);
                spellDescGenFields.Add(SpellDescriptionGen3);
                spellDescGenFields.Add(SpellDescriptionGen4);
                spellDescGenFields.Add(SpellDescriptionGen5);
                spellDescGenFields.Add(SpellDescriptionGen6);
                spellDescGenFields.Add(SpellDescriptionGen7);
                spellDescGenFields.Add(SpellDescriptionGen8);
                spellTooltipGenFields.Add(SpellTooltipGen0);
                spellTooltipGenFields.Add(SpellTooltipGen1);
                spellTooltipGenFields.Add(SpellTooltipGen2);
                spellTooltipGenFields.Add(SpellTooltipGen3);
                spellTooltipGenFields.Add(SpellTooltipGen4);
                spellTooltipGenFields.Add(SpellTooltipGen5);
                spellTooltipGenFields.Add(SpellTooltipGen6);
                spellTooltipGenFields.Add(SpellTooltipGen7);
                spellTooltipGenFields.Add(SpellTooltipGen8);

                RefreshAllUIElements();

                for (int i = 0; i < 32; ++i)
                {
                    uint mask = (uint)Math.Pow(2, i);

                    SpellMask11.Items.Add(new ThreadSafeCheckBox { Content = "0x" + mask.ToString("x8") });
                    SpellMask12.Items.Add(new ThreadSafeCheckBox { Content = "0x" + mask.ToString("x8") });
                    SpellMask13.Items.Add(new ThreadSafeCheckBox { Content = "0x" + mask.ToString("x8") });
                    SpellMask21.Items.Add(new ThreadSafeCheckBox { Content = "0x" + mask.ToString("x8") });
                    SpellMask22.Items.Add(new ThreadSafeCheckBox { Content = "0x" + mask.ToString("x8") });
                    SpellMask23.Items.Add(new ThreadSafeCheckBox { Content = "0x" + mask.ToString("x8") });
                    SpellMask31.Items.Add(new ThreadSafeCheckBox { Content = "0x" + mask.ToString("x8") });
                    SpellMask32.Items.Add(new ThreadSafeCheckBox { Content = "0x" + mask.ToString("x8") });
                    SpellMask33.Items.Add(new ThreadSafeCheckBox { Content = "0x" + mask.ToString("x8") });
                }

                foreach (ThreadSafeCheckBox cb in SpellMask11.Items) { cb.Checked += HandspellFamilyClassMask_Checked; cb.Unchecked += HandspellFamilyClassMask_Checked; }
                foreach (ThreadSafeCheckBox cb in SpellMask12.Items) { cb.Checked += HandspellFamilyClassMask_Checked; cb.Unchecked += HandspellFamilyClassMask_Checked; }
                foreach (ThreadSafeCheckBox cb in SpellMask13.Items) { cb.Checked += HandspellFamilyClassMask_Checked; cb.Unchecked += HandspellFamilyClassMask_Checked; }
                foreach (ThreadSafeCheckBox cb in SpellMask21.Items) { cb.Checked += HandspellFamilyClassMask_Checked; cb.Unchecked += HandspellFamilyClassMask_Checked; }
                foreach (ThreadSafeCheckBox cb in SpellMask22.Items) { cb.Checked += HandspellFamilyClassMask_Checked; cb.Unchecked += HandspellFamilyClassMask_Checked; }
                foreach (ThreadSafeCheckBox cb in SpellMask23.Items) { cb.Checked += HandspellFamilyClassMask_Checked; cb.Unchecked += HandspellFamilyClassMask_Checked; }
                foreach (ThreadSafeCheckBox cb in SpellMask31.Items) { cb.Checked += HandspellFamilyClassMask_Checked; cb.Unchecked += HandspellFamilyClassMask_Checked; }
                foreach (ThreadSafeCheckBox cb in SpellMask32.Items) { cb.Checked += HandspellFamilyClassMask_Checked; cb.Unchecked += HandspellFamilyClassMask_Checked; }
                foreach (ThreadSafeCheckBox cb in SpellMask33.Items) { cb.Checked += HandspellFamilyClassMask_Checked; cb.Unchecked += HandspellFamilyClassMask_Checked; }


                // TODO: This should happen when the language has been established 
                /*
                switch ((LocaleConstant)GetLanguage())
                {
                    case LocaleConstant.LOCALE_enUS:
                        TabItem_English.Focus();
                        break;
                    case LocaleConstant.LOCALE_koKR:
                        TabItem_Korean.Focus();
                        break;
                    case LocaleConstant.LOCALE_frFR:
                        TabItem_French.Focus();
                        break;
                    case LocaleConstant.LOCALE_deDE:
                        TabItem_Deutsch.Focus();
                        break;
                    case LocaleConstant.LOCALE_zhCN:
                        TabItem_Chinese.Focus();
                        break;
                    case LocaleConstant.LOCALE_zhTW:
                        TabItem_Taiwanese.Focus();
                        break;
                    case LocaleConstant.LOCALE_esES:
                        TabItem_Mexican.Focus();
                        break;
                    case LocaleConstant.LOCALE_esMX:
                        TabItem_Portuguese.Focus();
                        break;
                    case LocaleConstant.LOCALE_ruRU:
                        TabItem_Russian.Focus();
                        break;
                    default:
                        break;
                }
                */

                loadAllData();
            }

            catch (Exception ex)
            {
                HandleErrorMessage(ex.Message);
            }
        }

        private void HandspellFamilyClassMask_Checked(object obj, RoutedEventArgs e)
        {
            ThreadSafeComboBox father = (ThreadSafeComboBox)((ThreadSafeCheckBox)obj).Parent;

            uint Mask = 0;
            for (uint i = 0; i < 32; i++)
            {
                ThreadSafeCheckBox cb = (ThreadSafeCheckBox)father.Items.GetItemAt((int)i);
                Mask += cb.IsChecked == true ? (uint)Math.Pow(2, i) : 0;
            }
            father.Text = Mask.ToString();
        }

        #endregion

        public delegate void UpdateProgressFunc(double value);
        public delegate void UpdateTextFunc(string value);

        #region ImportExportSpellDBC
        private async void ImportExportSpellDbcButton(object sender, RoutedEventArgs e)
        {
            var window = new ImportExportWindow(adapter);
            var controller = await this.ShowProgressAsync(TryFindResource("Import/Export").ToString(), SafeTryFindResource("String1"));
            controller.SetCancelable(false);
            window.Show();
            window.Height += 40;
            window.Width /= 2;
            while (window.IsVisible && !window.IsDataSelected())
                await Task.Delay(100);
            if (window.IsVisible)
                window.Close();
            var isImport = window.BindingImportList.Count > 0;
            var archiveName = window.MpqArchiveName;
            var bindingList = isImport ? window.BindingImportList : window.BindingExportList;
            var manager = DBCManager.GetInstance();
            foreach (var bindingName in bindingList)
            {
                try
                {
                    controller.SetMessage($"{(isImport ? "Importing" : "Exporting")} {bindingName}.dbc...");
                    manager.ClearDbcBinding(bindingName);
                    var abstractDbc = manager.FindDbcForBinding(bindingName);
                    if (abstractDbc == null)
                    {
                        try
                        {
                            abstractDbc = new GenericDbc($"{ Config.DbcDirectory }\\{ bindingName }.dbc");
                        }
                        catch (Exception exception)
                        {
                            Logger.Info($"ERROR: Failed to load {Config.DbcDirectory}\\{bindingName}.dbc: {exception.Message}\n{exception}\n{exception.InnerException}");
                            ShowFlyoutMessage($"Failed to load {Config.DbcDirectory}\\{bindingName}.dbc");
                            continue;
                        }
                    }
                    if (isImport && !abstractDbc.HasData())
                        abstractDbc.ReloadContents();
                    if (isImport)
                        await abstractDbc.ImportToSql(adapter, controller.SetProgress, "ID", bindingName);
                    else
                        await abstractDbc.ExportToDbc(adapter, controller.SetProgress, "ID", bindingName);
                }
                catch (Exception exception)
                {
                    HandleErrorMessage(exception.GetType() + ": " + exception.Message + "\n" + exception.InnerException + "\n" + exception.StackTrace);
                }
            }
            controller.SetMessage(SafeTryFindResource("ReloadingUI"));
            loadAllRequiredDbcs();
            await controller.CloseAsync();
            if (!string.IsNullOrEmpty(archiveName))
            {
                var exportList = new List<string>();
                Directory.EnumerateFiles("Export")
                    .Where((dbcFile) => dbcFile.EndsWith(".dbc"))
                    .ToList()
                    .ForEach(exportList.Add);
                var mpqExport = new MpqExport();
                mpqExport.CreateMpqFromDbcFileList(archiveName, exportList);
            }
            PopulateSelectSpell();
        }

        #endregion

        #region ConfigButton
        private ConfigWindow ConfigWindowInstance;

        private void ConfigButtonClick(object sender, RoutedEventArgs e)
        {
            if (ConfigWindowInstance != null && ConfigWindowInstance.IsLoaded)
            {
                ConfigWindowInstance.Activate();
                return;
            }
            var window = new ConfigWindow(adapter is MySQL ? 
                ConfigWindow.DatabaseIdentifier.MySQL : ConfigWindow.DatabaseIdentifier.SQLite);
            window.Show();
            window.Width *= 0.6;
            window.Height *= 0.7;
            ConfigWindowInstance = window;
        }
        #endregion

        #region InitialiseMemberVariables
        private void loadAllRequiredDbcs()
        {
            // Load required DBC's. First the ones with dependencies and inject them into the manager
            var manager = DBCManager.GetInstance();
            manager.LoadRequiredDbcs();
            if (WoWVersionManager.IsWotlkOrGreaterSelected)
            {
                manager.InjectLoadedDbc("AreaGroup", new AreaGroup(((AreaTable)manager.FindDbcForBinding("AreaTable")).Lookups));
                manager.InjectLoadedDbc("SpellDifficulty", new SpellDifficulty(adapter, GetLocale()));
            }
            manager.InjectLoadedDbc("SpellIcon", new SpellIconDBC(this, adapter));
            spellFamilyClassMaskParser = new SpellFamilyClassMaskParser(this);
        }

        private async void loadAllData()
        {
            await GetConfig();
            if (!Config.IsInit)
            {
                await this.ShowMessageAsync(SafeTryFindResource("ERROR"), SafeTryFindResource("String2"));
                return;
            }
            var controller = await this.ShowProgressAsync(SafeTryFindResource("PleaseWait"), SafeTryFindResource("PleaseWait_2"));
            controller.SetCancelable(false);
            await Task.Delay(1000);
            try
            {
                switch (Config.connectionType)
                {
                    case Config.ConnectionType.MySQL:
                        adapter = new MySQL();
                        break;
                    case Config.ConnectionType.SQLite:
                        adapter = new SQLite();
                        break;
                    default:
                        throw new Exception("Unknown connection type, valid types: MySQL, SQLite");
                }
                adapter.CreateAllTablesFromBindings();
            }
            catch (Exception e)
            {
                await controller.CloseAsync();
                await this.ShowMessageAsync(SafeTryFindResource("ERROR"),
                    $"{SafeTryFindResource("Input_MySQL_Error")}\n{e.Message + "\n" + e.InnerException?.Message}");
                return;
            }
            try
            {
                loadAllRequiredDbcs();
            }
            catch (MySqlException e)
            {
                await controller.CloseAsync();
                await this.ShowMessageAsync(SafeTryFindResource("ERROR"),
                    $"{SafeTryFindResource("LoadDBCFromBinding_Error_1")}: {e.Message}\n\n{e}\n{e.InnerException}");
                return;
            }
            catch (SQLiteException e)
            {
                await controller.CloseAsync();
                await this.ShowMessageAsync(SafeTryFindResource("ERROR"),
                    $"{SafeTryFindResource("LoadDBCFromBinding_Error_1")}: {e.Message}\n\n{e}\n{e.InnerException}");
                return;
            }
            catch (Exception e)
            {
                await controller.CloseAsync();
                await this.ShowMessageAsync(SafeTryFindResource("ERROR"),
                    $"{SafeTryFindResource("LoadDBCFromBinding_Error_1")}: {e.Message}\n\n{e}\n{e.InnerException}");
                return;
            }

            try
            {
                spellTable.Columns.Add("id", typeof(uint));
                spellTable.Columns.Add("SpellName" + GetLocale(), typeof(string));
                spellTable.Columns.Add("Icon", typeof(uint));

                // Populate UI based on DBC data
                Category.ItemsSource = ConvertBoxListToLabels(((SpellCategory)
                    DBCManager.GetInstance().FindDbcForBinding("SpellCategory")).GetAllBoxes());
                DispelType.ItemsSource = ConvertBoxListToLabels(((SpellDispelType)
                    DBCManager.GetInstance().FindDbcForBinding("SpellDispelType")).GetAllBoxes());
                MechanicType.ItemsSource = ConvertBoxListToLabels(((SpellMechanic)
                    DBCManager.GetInstance().FindDbcForBinding("SpellMechanic")).GetAllBoxes());
                RequiresSpellFocus.ItemsSource = ConvertBoxListToLabels(((SpellFocusObject)
                    DBCManager.GetInstance().FindDbcForBinding("SpellFocusObject")).GetAllBoxes());
                CastTime.ItemsSource = ConvertBoxListToLabels(((SpellCastTimes)
                    DBCManager.GetInstance().FindDbcForBinding("SpellCastTimes")).GetAllBoxes());
                Duration.ItemsSource = ConvertBoxListToLabels(((SpellDuration)
                    DBCManager.GetInstance().FindDbcForBinding("SpellDuration")).GetAllBoxes());
                Range.ItemsSource = ConvertBoxListToLabels(((SpellRange)
                    DBCManager.GetInstance().FindDbcForBinding("SpellRange")).GetAllBoxes());
                var radiusLabels = ConvertBoxListToLabels(((SpellRadius)
                    DBCManager.GetInstance().FindDbcForBinding("SpellRadius")).GetAllBoxes());
                RadiusIndex1.ItemsSource = radiusLabels;
                RadiusIndex2.ItemsSource = radiusLabels;
                RadiusIndex3.ItemsSource = radiusLabels;
                EquippedItemClass.ItemsSource = ConvertBoxListToLabels(((ItemClass)
                    DBCManager.GetInstance().FindDbcForBinding("ItemClass")).GetAllBoxes());
                var isTbcOrGreater = WoWVersionManager.IsTbcOrGreaterSelected;
                var isWotlkOrGreater = WoWVersionManager.IsWotlkOrGreaterSelected;
                if (isTbcOrGreater)
                {
                    var totemLabels = ConvertBoxListToLabels(((TotemCategory)
                        DBCManager.GetInstance().FindDbcForBinding("TotemCategory")).GetAllBoxes());
                    TotemCategory1.ItemsSource = totemLabels;
                    TotemCategory2.ItemsSource = totemLabels;
                }
                if (isWotlkOrGreater)
                {
                    AreaGroup.ItemsSource = ConvertBoxListToLabels(((AreaGroup)
                        DBCManager.GetInstance().FindDbcForBinding("AreaGroup")).GetAllBoxes());
                    Difficulty.ItemsSource = ConvertBoxListToLabels(((SpellDifficulty)
                        DBCManager.GetInstance().FindDbcForBinding("SpellDifficulty")).GetAllBoxes());
                    RuneCost.ItemsSource = ConvertBoxListToLabels(((SpellRuneCost)
                        DBCManager.GetInstance().FindDbcForBinding("SpellRuneCost")).GetAllBoxes());
                    SpellDescriptionVariables.ItemsSource = ConvertBoxListToLabels(((SpellDescriptionVariables)
                        DBCManager.GetInstance().FindDbcForBinding("SpellDescriptionVariables")).GetAllBoxes());
                }
                AreaGroup.IsEnabled = isWotlkOrGreater;
                Difficulty.IsEnabled = isWotlkOrGreater;
                TotemCategory1.IsEnabled = isTbcOrGreater;
                TotemCategory2.IsEnabled = isTbcOrGreater;
                RuneCost.IsEnabled = isWotlkOrGreater;
                SpellDescriptionVariables.IsEnabled = isWotlkOrGreater;

                VisualSettingsGrid.ContextMenu = new VisualContextMenu((item, args) => PasteVisualKitAction());
                VisualEffectsListGrid.ContextMenu = new VisualContextMenu((item, args) => PasteVisualEffectAction());
                InitialiseSpellVisualEffectList();

                prepareIconEditor();
            }
            catch (Exception e)
            {
                await controller.CloseAsync();
                await this.ShowMessageAsync(SafeTryFindResource("ERROR"),
                    $"{SafeTryFindResource("LoadDBCFromBinding_Error_1")}\n\n{e}\n{e.InnerException}");
                return;
            }

            await controller.CloseAsync();
            PopulateSelectSpell();
        }

        private List<Label> ConvertBoxListToLabels(List<DBCBoxContainer> boxes) => boxes.Select(entry => entry.ItemLabel()).ToList();

        private async Task GetConfig()
        {
            if (!Config.IsInit)
            {
                var settings = new MetroDialogSettings
                {
                    AffirmativeButtonText = "SQLite",
                    NegativeButtonText = "MySQL",
                    AnimateHide = true,
                    AnimateShow = true,
                    ColorScheme = MetroDialogColorScheme.Accented
                };
                MessageDialogResult exitCode = await this.ShowMessageAsync(SafeTryFindResource("SpellEditor"),
                    SafeTryFindResource("Welcome"),
                    MessageDialogStyle.AffirmativeAndNegative, settings);
                bool isSqlite = exitCode == MessageDialogResult.Affirmative;

                if (!isSqlite)
                {
                    if (Config.NeedInitMysql)
                    {
                        string host = await this.ShowInputAsync(SafeTryFindResource("Input_MySQL_Details"), SafeTryFindResource("Input_MySQL_Details_1"));
                        string user = await this.ShowInputAsync(SafeTryFindResource("Input_MySQL_Details"), SafeTryFindResource("Input_MySQL_Details_2"));
                        string pass = await this.ShowInputAsync(SafeTryFindResource("Input_MySQL_Details"), SafeTryFindResource("Input_MySQL_Details_3"));
                        string port = await this.ShowInputAsync(SafeTryFindResource("Input_MySQL_Details"), SafeTryFindResource("Input_MySQL_Details_4"));
                        string db = await this.ShowInputAsync(SafeTryFindResource("Input_MySQL_Details"), SafeTryFindResource("Input_MySQL_Details_5"));
                        
                        if (host == null || user == null || pass == null || port == null || db == null ||
                            host.Length == 0 || user.Length == 0 || port.Length == 0 || db.Length == 0 ||
                            !uint.TryParse(port, out var result))
                        {
                            throw new Exception(SafeTryFindResource("Input_MySQL_Error_2"));
                        }

                        Config.Host = host;
                        Config.User = user;
                        Config.Pass = pass;
                        Config.Port = port;
                        Config.Database = db;
                    }
                }
                Config.connectionType = isSqlite ? Config.ConnectionType.SQLite : Config.ConnectionType.MySQL;
                Config.IsInit = true;
            }
        }
        #endregion

        #region KeyHandlers
        private volatile bool imageLoadEventRunning;

        private void _KeyUp(object sender, KeyEventArgs e)
        {
            if (sender == FilterSpellNames && e.Key == Key.Back)
            {
                _KeyDown(sender, new KeyEventArgs(Keyboard.PrimaryDevice, Keyboard.PrimaryDevice.ActiveSource, 0, Key.Space));
            }
            else if (sender == FilterIcons && e.Key == Key.Back)
            {
                _KeyDown(sender, new KeyEventArgs(Keyboard.PrimaryDevice, Keyboard.PrimaryDevice.ActiveSource, 0, Key.Space));
            }
        }

        private async void _KeyDown(object sender, KeyEventArgs e)
        {
            if (sender == this)
            {
                if (e.Key == Key.Escape)
                {
                    MetroDialogSettings settings = new MetroDialogSettings
                    {
                        AffirmativeButtonText = SafeTryFindResource("Yes"),
                        NegativeButtonText = SafeTryFindResource("No")
                    };


                    MessageDialogStyle style = MessageDialogStyle.AffirmativeAndNegative;
                    MessageDialogResult exitCode = await this.ShowMessageAsync(SafeTryFindResource("SpellEditor"), SafeTryFindResource("Exit"), style, settings);

                    if (exitCode == MessageDialogResult.Affirmative)
                    {
                        Environment.Exit(0x1);
                    }
                }
                else if (Keyboard.IsKeyDown(Key.LeftCtrl) && Keyboard.IsKeyDown(Key.S))
                {
                    Button_Click(SaveSpellChanges, e);
                }
            }
            else if (sender == NavigateToSpell)
            {
                if (e.Key != Key.Enter)
                {
                    return;
                }
                try
                {
                    TextBox box = (TextBox)sender;

                    int ID = int.Parse(box.Text);

                    int count = 0;
                    foreach (StackPanel obj in SelectSpell.Items)
                    {
                        foreach (var item in obj.Children)
                            if (item is TextBlock tb)
                            {
                                if (int.Parse(tb.Text.Split(' ')[1]) == ID)
                                {
                                    SelectSpell.SelectedIndex = count;
                                    SelectSpell.ScrollIntoView(obj);

                                    break;
                                }
                            }
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    HandleErrorMessage(ex.Message);
                }
            }
            else if (sender == FilterSpellNames)
            {
                if (imageLoadEventRunning)
                    return;
                imageLoadEventRunning = true;
                var locale = GetLocale();
                var input = FilterSpellNames.Text.ToLower();
                var badInput = string.IsNullOrEmpty(input);
                if (badInput && spellTable.Rows.Count == SelectSpell.Items.Count)
                {
                    imageLoadEventRunning = false;
                    return;
                }

                ICollectionView view = CollectionViewSource.GetDefaultView(SelectSpell.Items);
                view.Filter = o =>
                {
                    var panel = (StackPanel) o;
                    using (var enumerator = panel.GetChildObjects().GetEnumerator())
                    {
                        while (enumerator.MoveNext())
                        {
                            if (!(enumerator.Current is TextBlock block))
                                continue;

                            var name = block.Text;
                            var spellName = name.Substring(name.IndexOf(' ', 4) + 1);
                            return spellName.ToLower().Contains(input);
                        }
                    }
                    return false;
                };

                imageLoadEventRunning = false;
            }
            else if (sender == FilterIcons)
            {
                var input = FilterIcons.Text.ToLower();
                foreach (Image image in IconGrid.Children)
                {
                    var name = image.ToolTip.ToString().ToLower();
                    image.Visibility = name.Contains(input) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
        #endregion

        #region ButtonClicks (and load spell god-function)
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            if (adapter == null)
            {
                loadAllData();
                return;
            }
            
            if (sender == TruncateTable)
            {
                MetroDialogSettings settings = new MetroDialogSettings
                {
                    AffirmativeButtonText = SafeTryFindResource("Yes"),
                    NegativeButtonText = SafeTryFindResource("No")
                };

                MessageDialogStyle style = MessageDialogStyle.AffirmativeAndNegative;
                var res = await this.ShowMessageAsync(SafeTryFindResource("TruncateTable1"), SafeTryFindResource("TruncateTable2"), style, settings);
                if (res == MessageDialogResult.Affirmative)
                {
                    foreach (var binding in BindingManager.GetInstance().GetAllBindings())
                        adapter.Execute($"drop table `{binding.Name}`");
                    adapter.CreateAllTablesFromBindings();
                    PopulateSelectSpell();
                }
                return;
            }

            if (sender == InsertANewRecord)
            {
                MetroDialogSettings settings = new MetroDialogSettings
                {
                    AffirmativeButtonText = SafeTryFindResource("Yes"),
                    NegativeButtonText = SafeTryFindResource("No")
                };


                MessageDialogStyle style = MessageDialogStyle.AffirmativeAndNegative;
                MessageDialogResult copySpell = await this.ShowMessageAsync(SafeTryFindResource("SpellEditor"), SafeTryFindResource("CopySpellRecord1"), style, settings);

                uint oldIDIndex = uint.MaxValue;

                if (copySpell == MessageDialogResult.Affirmative)
                {
                    string inputCopySpell = await this.ShowInputAsync(SafeTryFindResource("SpellEditor"), SafeTryFindResource("CopySpellRecord2"));
                    if (inputCopySpell == null) { return; }

                    if (!uint.TryParse(inputCopySpell, out var oldID))
                    {
                        HandleErrorMessage(SafeTryFindResource("CopySpellRecord3"));
                        return;
                    }
                    oldIDIndex = oldID;
                }

                string inputNewRecord = await this.ShowInputAsync(SafeTryFindResource("SpellEditor"), SafeTryFindResource("CopySpellRecord4"));
                if (inputNewRecord == null) { return; }

                if (!uint.TryParse(inputNewRecord, out var newID))
                {
                    HandleErrorMessage(SafeTryFindResource("CopySpellRecord5"));
                    return;
                }

                if (uint.Parse(adapter.Query($"SELECT COUNT(*) FROM `spell` WHERE `ID` = '{newID}'").Rows[0][0].ToString()) > 0)
                {
                    HandleErrorMessage(SafeTryFindResource("CopySpellRecord6"));
                    return;
                }

                if (oldIDIndex != uint.MaxValue)
                {
                    // Copy old spell to new spell
                    var row = adapter.Query($"SELECT * FROM `spell` WHERE `ID` = '{oldIDIndex}' LIMIT 1").Rows[0];
                    StringBuilder str = new StringBuilder();
                    str.Append($"INSERT INTO `spell` VALUES ('{newID}'");
                    for (int i = 1; i < row.Table.Columns.Count; ++i)
                        str.Append($", \"{row[i]}\"");
                    str.Append(")");
                    adapter.Execute(str.ToString());
                }
                else
                {
                    // Create new spell
                    HandleErrorMessage(SafeTryFindResource("CopySpellRecord7"));
                    return;
                }

                PopulateSelectSpell();

                ShowFlyoutMessage(string.Format(SafeTryFindResource("CopySpellRecord8"), inputNewRecord));
                return;
            }

            if (sender == DeleteARecord)
            {
                string input = await this.ShowInputAsync(SafeTryFindResource("SpellEditor"), SafeTryFindResource("DeleteSpellRecord1"));

                if (input == null) { return; }

                if (!uint.TryParse(input, out var spellID))
                {
                    HandleErrorMessage(SafeTryFindResource("DeleteSpellRecord2"));
                    return;
                }

                adapter.Execute($"DELETE FROM `spell` WHERE `ID` = '{spellID}'");
                
                selectedID = 0;

                PopulateSelectSpell();

                ShowFlyoutMessage(SafeTryFindResource("DeleteSpellRecord3"));
                return;
            }

            if (sender == SaveSpellChanges)
            {
                string query = $"SELECT * FROM `spell` WHERE `ID` = '{selectedID}' LIMIT 1";
                var q = adapter.Query(query);
                if (q.Rows.Count == 0)
                    return;
                var row = q.Rows[0];
                var isWotlkOrGreater = WoWVersionManager.IsWotlkOrGreaterSelected;
                var isTbcOrGreater = WoWVersionManager.IsTbcOrGreaterSelected;
                row.BeginEdit();
                try
                {

                    uint maskk = 0;
                    uint flagg = 1;

                    foreach (ThreadSafeCheckBox attribute0 in attributes0)
                    {
                        if (attribute0.IsChecked.Value) { maskk += flagg; }
                        flagg += flagg;
                    }

                    row["Attributes"] = maskk;

                    maskk = 0;
                    flagg = 1;

                    foreach (ThreadSafeCheckBox attribute1 in attributes1)
                    {
                        if (attribute1.IsChecked.Value) { maskk += flagg; }
                        flagg += flagg;
                    }

                    row["AttributesEx"] = maskk;

                    maskk = 0;
                    flagg = 1;

                    foreach (ThreadSafeCheckBox attribute2 in attributes2)
                    {
                        if (attribute2.IsChecked.Value) { maskk += flagg; }
                        flagg += flagg;
                    }

                    row["AttributesEx2"] = maskk;


                    maskk = 0;
                    flagg = 1;

                    foreach (ThreadSafeCheckBox attribute3 in attributes3)
                    {
                        if (attribute3.IsChecked.Value) { maskk += flagg; }
                        flagg += flagg;
                    }

                    row["AttributesEx3"] = maskk;

                    maskk = 0;
                    flagg = 1;

                    foreach (ThreadSafeCheckBox attribute4 in attributes4)
                    {
                        if (attribute4.IsChecked.Value) { maskk += flagg; }

                        flagg += flagg;
                    }

                    row["AttributesEx4"] = maskk;

                    maskk = 0;
                    flagg = 1;

                    if (isTbcOrGreater)
                    {
                        foreach (ThreadSafeCheckBox attribute5 in attributes5)
                        {
                            if (attribute5.IsChecked.Value) { maskk += flagg; }

                            flagg += flagg;
                        }

                        row["AttributesEx5"] = maskk;

                        maskk = 0;
                        flagg = 1;

                        foreach (ThreadSafeCheckBox attribute6 in attributes6)
                        {
                            if (attribute6.IsChecked.Value) { maskk += flagg; }

                            flagg += flagg;
                        }

                        row["AttributesEx6"] = maskk;

                        if (stancesBoxes[0].IsChecked.Value)
                        {
                            row["Stances"] = 0;
                        }
                        else
                        {
                            uint mask = 0;
                            uint flag = 1;

                            for (int f = 1; f < stancesBoxes.Count; ++f)
                            {
                                if (stancesBoxes[f].IsChecked.Value) { mask += flag; }

                                flag += flag;
                            }

                            row["Stances"] = mask;
                        }
                    }

                    if (isWotlkOrGreater)
                    {
                        maskk = 0;
                        flagg = 1;

                        foreach (ThreadSafeCheckBox attribute7 in attributes7)
                        {
                            if (attribute7.IsChecked.Value) { maskk += flagg; }

                            flagg += flagg;
                        }

                        row["AttributesEx7"] = maskk;
                    }

                    if (targetBoxes[0].IsChecked.Value)
                    {
                        row["Targets"] = 0;
                    }
                    else
                    {
                        uint mask = 0;
                        uint flag = 1;

                        for (int f = 1; f < targetBoxes.Count; ++f)
                        {
                            if (targetBoxes[f].IsChecked.Value) { mask += flag; }

                            flag += flag;
                        }

                        row["Targets"] = mask;
                    }

                    if (targetCreatureTypeBoxes[0].IsChecked.Value)
                    {
                        row["TargetCreatureType"] = 0;
                    }
                    else
                    {
                        uint mask = 0;
                        uint flag = 1;

                        for (int f = 1; f < targetCreatureTypeBoxes.Count; ++f)
                        {
                            if (targetCreatureTypeBoxes[f].IsChecked.Value) { mask += flag; }
                            flag += flag;
                        }

                        row["TargetCreatureType"] = mask;
                    }

                    if (isTbcOrGreater)
                    {
                        row["FacingCasterFlags"] = FacingFrontFlag.IsChecked.Value ? (uint)0x1 : (uint)0x0;

                        switch (CasterAuraState.SelectedIndex)
                        {
                            case 0: // None
                                row["CasterAuraState"] = 0;
                                break;
                            case 1: // Defense
                                row["CasterAuraState"] = 1;
                                break;
                            case 2: // Healthless 20%
                                row["CasterAuraState"] = 2;
                                break;
                            case 3: // Berserking
                                row["CasterAuraState"] = 3;
                                break;
                            case 4: // Judgement
                                row["CasterAuraState"] = 5;
                                break;
                            case 5: // Hunter Parry
                                row["CasterAuraState"] = 7;
                                break;
                            case 6: // Victory Rush
                                row["CasterAuraState"] = 10;
                                break;
                            case 7: // Unknown 1
                                row["CasterAuraState"] = 11;
                                break;
                            case 8: // Healthless 35%
                                row["CasterAuraState"] = 13;
                                break;
                            case 9: // Enrage
                                row["CasterAuraState"] = 17;
                                break;
                            case 10: // Unknown 2
                                row["CasterAuraState"] = 22;
                                break;
                            case 11: // Health Above 75%
                                row["CasterAuraState"] = 23;
                                break;
                        }
                    }

                    switch (TargetAuraState.SelectedIndex)
                    {
                        case 0: // None
                            row["TargetAuraState"] = 0;
                            break;
                        case 1: // Healthless 20%
                            row["TargetAuraState"] = 2;
                            break;
                        case 2: // Berserking
                            row["TargetAuraState"] = 3;
                            break;
                        case 3: // Healthless 35%
                            row["TargetAuraState"] = 13;
                            break;
                        case 4: // Conflagrate
                            row["TargetAuraState"] = 14;
                            break;
                        case 5: // Swiftmend
                            row["TargetAuraState"] = 15;
                            break;
                        case 6: // Deadly Poison
                            row["TargetAuraState"] = 16;
                            break;
                        case 7: // Bleeding
                            row["TargetAuraState"] = 18;
                            break;
                    }

                    row["RecoveryTime"] = uint.Parse(RecoveryTime.Text);
                    row["CategoryRecoveryTime"] = uint.Parse(CategoryRecoveryTime.Text);

                    if (interrupts1[0].IsChecked.Value)
                    {
                        row["InterruptFlags"] = 0;
                    }
                    else
                    {
                        uint mask = 0;
                        uint flag = 1;

                        for (int f = 1; f < interrupts1.Count; ++f)
                        {
                            if (interrupts1[f].IsChecked.Value) { mask += flag; }

                            flag += flag;
                        }

                        row["InterruptFlags"] = mask;
                    }

                    if (interrupts2[0].IsChecked.Value)
                    {
                        row["AuraInterruptFlags"] = 0;
                    }
                    else
                    {
                        uint mask = 0;
                        uint flag = 1;

                        for (int f = 1; f < interrupts2.Count; ++f)
                        {
                            if (interrupts2[f].IsChecked.Value) { mask += flag; }

                            flag += flag;
                        }

                        row["AuraInterruptFlags"] = mask;
                    }

                    if (interrupts3[0].IsChecked.Value)
                    {
                        row["ChannelInterruptFlags"] = 0;
                    }
                    else
                    {
                        uint mask = 0;
                        uint flag = 1;

                        for (int f = 1; f < interrupts3.Count; ++f)
                        {
                            if (interrupts3[f].IsChecked.Value) { mask += flag; }

                            flag += flag;
                        }

                        row["ChannelInterruptFlags"] = mask;
                    }

                    if (procBoxes[0].IsChecked.Value)
                    {
                        row["ProcFlags"] = 0;
                    }
                    else
                    {
                        uint mask = 0;
                        uint flag = 1;

                        for (int f = 1; f < procBoxes.Count; ++f)
                        {
                            if (procBoxes[f].IsChecked.Value) { mask += flag; }

                            flag += flag;
                        }

                        row["ProcFlags"] = mask;
                    }

                    row["ProcChance"] = uint.Parse(ProcChance.Text);
                    row["ProcCharges"] = uint.Parse(ProcCharges.Text);
                    row["MaximumLevel"] = uint.Parse(MaximumLevel.Text);
                    row["BaseLevel"] = uint.Parse(BaseLevel.Text);
                    row["SpellLevel"] = uint.Parse(SpellLevel.Text);
                    // Handle 'Health' power type manually
                    row["PowerType"] = PowerType.SelectedIndex == 13 ? (uint.MaxValue - 1) : (uint)PowerType.SelectedIndex;
                    row["ManaCost"] = uint.Parse(PowerCost.Text);
                    row["ManaCostPerLevel"] = uint.Parse(ManaCostPerLevel.Text);
                    row["ManaPerSecond"] = uint.Parse(ManaCostPerSecond.Text);
                    row["ManaPerSecondPerLevel"] = uint.Parse(PerSecondPerLevel.Text);
                    row["Speed"] = float.Parse(Speed.Text);
                    row["StackAmount"] = uint.Parse(Stacks.Text);
                    row["Totem1"] = uint.Parse(Totem1.Text);
                    row["Totem2"] = uint.Parse(Totem2.Text);
                    row["Reagent1"] = int.Parse(Reagent1.Text);
                    row["Reagent2"] = int.Parse(Reagent2.Text);
                    row["Reagent3"] = int.Parse(Reagent3.Text);
                    row["Reagent4"] = int.Parse(Reagent4.Text);
                    row["Reagent5"] = int.Parse(Reagent5.Text);
                    row["Reagent6"] = int.Parse(Reagent6.Text);
                    row["Reagent7"] = int.Parse(Reagent7.Text);
                    row["Reagent8"] = int.Parse(Reagent8.Text);
                    row["ReagentCount1"] = uint.Parse(ReagentCount1.Text);
                    row["ReagentCount2"] = uint.Parse(ReagentCount2.Text);
                    row["ReagentCount3"] = uint.Parse(ReagentCount3.Text);
                    row["ReagentCount4"] = uint.Parse(ReagentCount4.Text);
                    row["ReagentCount5"] = uint.Parse(ReagentCount5.Text);
                    row["ReagentCount6"] = uint.Parse(ReagentCount6.Text);
                    row["ReagentCount7"] = uint.Parse(ReagentCount7.Text);
                    row["ReagentCount8"] = uint.Parse(ReagentCount8.Text);

                    if (equippedItemInventoryTypeMaskBoxes[0].IsChecked.Value)
                    {
                        row["EquippedItemInventoryTypeMask"] = 0;
                    }
                    else
                    {
                        uint mask = 0;
                        uint flag = 1;

                        for (int f = 0; f < equippedItemInventoryTypeMaskBoxes.Count; ++f)
                        {
                            if (equippedItemInventoryTypeMaskBoxes[f].IsChecked.Value) { mask += flag; }

                            flag += flag;
                        }

                        row["EquippedItemInventoryTypeMask"] = (int)mask;
                    }

                    if (EquippedItemClass.Text == SafeTryFindResource("None"))
                    {
                        row["EquippedItemClass"] = -1;
                        row["EquippedItemSubClassMask"] = 0;
                    }
                    else
                    {
                        uint Mask = 0;
                        for (int i = 0; i < equippedItemSubClassMaskBoxes.Count; i++)
                            Mask += equippedItemSubClassMaskBoxes[i].IsChecked.Value ? (uint)Math.Pow(2, i) : 0;
                        
                        row["EquippedItemSubClassMask"] = Mask;
                    }

                    row["Effect1"] = (uint)SpellEffect1.SelectedIndex;
                    row["Effect2"] = (uint)SpellEffect2.SelectedIndex;
                    row["Effect3"] = (uint)SpellEffect3.SelectedIndex;
                    row["EffectDieSides1"] = int.Parse(DieSides1.Text);
                    row["EffectDieSides2"] = int.Parse(DieSides2.Text);
                    row["EffectDieSides3"] = int.Parse(DieSides3.Text);
                    row["EffectRealPointsPerLevel1"] = float.Parse(BasePointsPerLevel1.Text);
                    row["EffectRealPointsPerLevel2"] = float.Parse(BasePointsPerLevel2.Text);
                    row["EffectRealPointsPerLevel3"] = float.Parse(BasePointsPerLevel3.Text);
                    row["EffectBasePoints1"] = int.Parse(BasePoints1.Text);
                    row["EffectBasePoints2"] = int.Parse(BasePoints2.Text);
                    row["EffectBasePoints3"] = int.Parse(BasePoints3.Text);
                    row["EffectMechanic1"] = (uint)Mechanic1.SelectedIndex;
                    row["EffectMechanic2"] = (uint)Mechanic2.SelectedIndex;
                    row["EffectMechanic3"] = (uint)Mechanic3.SelectedIndex;
                    row["EffectImplicitTargetA1"] = (uint)TargetA1.SelectedIndex;
                    row["EffectImplicitTargetA2"] = (uint)TargetA2.SelectedIndex;
                    row["EffectImplicitTargetA3"] = (uint)TargetA3.SelectedIndex;
                    row["EffectImplicitTargetB1"] = (uint)TargetB1.SelectedIndex;
                    row["EffectImplicitTargetB2"] = (uint)TargetB2.SelectedIndex;
                    row["EffectImplicitTargetB3"] = (uint)TargetB3.SelectedIndex;
                    row["EffectApplyAuraName1"] = (uint)ApplyAuraName1.SelectedIndex;
                    row["EffectApplyAuraName2"] = (uint)ApplyAuraName2.SelectedIndex;
                    row["EffectApplyAuraName3"] = (uint)ApplyAuraName3.SelectedIndex;
                    row["EffectAmplitude1"] = uint.Parse(Amplitude1.Text);
                    row["EffectAmplitude2"] = uint.Parse(Amplitude2.Text);
                    row["EffectAmplitude3"] = uint.Parse(Amplitude3.Text);
                    row["EffectMultipleValue1"] = float.Parse(MultipleValue1.Text);
                    row["EffectMultipleValue2"] = float.Parse(MultipleValue1.Text);
                    row["EffectMultipleValue3"] = float.Parse(MultipleValue1.Text);
                    row["EffectChainTarget1"] = uint.Parse(ChainTarget1.Text);
                    row["EffectChainTarget2"] = uint.Parse(ChainTarget2.Text);
                    row["EffectChainTarget3"] = uint.Parse(ChainTarget3.Text);
                    row["EffectItemType1"] = uint.Parse(ItemType1.Text);
                    row["EffectItemType2"] = uint.Parse(ItemType2.Text);
                    row["EffectItemType3"] = uint.Parse(ItemType3.Text);
                    row["EffectMiscValue1"] = int.Parse(MiscValueA1.Text);
                    row["EffectMiscValue2"] = int.Parse(MiscValueA2.Text);
                    row["EffectMiscValue3"] = int.Parse(MiscValueA3.Text);
                    if (isTbcOrGreater)
                    {
                        row["EffectMiscValueB1"] = int.Parse(MiscValueB1.Text);
                        row["EffectMiscValueB2"] = int.Parse(MiscValueB2.Text);
                        row["EffectMiscValueB3"] = int.Parse(MiscValueB3.Text);
                    }
                    row["EffectTriggerSpell1"] = uint.Parse(TriggerSpell1.Text);
                    row["EffectTriggerSpell2"] = uint.Parse(TriggerSpell2.Text);
                    row["EffectTriggerSpell3"] = uint.Parse(TriggerSpell3.Text);
                    row["EffectPointsPerComboPoint1"] = float.Parse(PointsPerComboPoint1.Text);
                    row["EffectPointsPerComboPoint2"] = float.Parse(PointsPerComboPoint2.Text);
                    row["EffectPointsPerComboPoint3"] = float.Parse(PointsPerComboPoint3.Text);
                    if (isWotlkOrGreater)
                    {
                        row["EffectSpellClassMaskA1"] = uint.Parse(SpellMask11.Text);
                        row["EffectSpellClassMaskA2"] = uint.Parse(SpellMask21.Text);
                        row["EffectSpellClassMaskA3"] = uint.Parse(SpellMask31.Text);
                        row["EffectSpellClassMaskB1"] = uint.Parse(SpellMask12.Text);
                        row["EffectSpellClassMaskB2"] = uint.Parse(SpellMask22.Text);
                        row["EffectSpellClassMaskB3"] = uint.Parse(SpellMask32.Text);
                        row["EffectSpellClassMaskC1"] = uint.Parse(SpellMask13.Text);
                        row["EffectSpellClassMaskC2"] = uint.Parse(SpellMask23.Text);
                        row["EffectSpellClassMaskC3"] = uint.Parse(SpellMask33.Text);
                    }
                    row["SpellVisual1"] = uint.Parse(SpellVisual1.Text);
                    row["SpellVisual2"] = uint.Parse(SpellVisual2.Text);
                    row["ManaCostPercentage"] = uint.Parse(ManaCostPercent.Text);
                    row["StartRecoveryCategory"] = uint.Parse(StartRecoveryCategory.Text);
                    row["StartRecoveryTime"] = uint.Parse(StartRecoveryTime.Text);
                    row["MaximumTargetLevel"] = uint.Parse(MaxTargetsLevel.Text);
                    // Before WOTLK there are only two flags, we misnamed them in WOTLK as the last flag handles A3/B3/C3 of the affecting spells
                    if (!isWotlkOrGreater)
                    {
                        row["SpellFamilyName"] = uint.Parse(SpellFamilyName.Text);
                        row["SpellFamilyFlags1"] = uint.Parse(SpellFamilyFlags.Text);
                        row["SpellFamilyFlags2"] = uint.Parse(SpellFamilyFlags1.Text);
                    }
                    else
                    {
                        row["SpellFamilyName"] = uint.Parse(SpellFamilyName.Text);
                        row["SpellFamilyFlags"] = uint.Parse(SpellFamilyFlags.Text);
                        row["SpellFamilyFlags1"] = uint.Parse(SpellFamilyFlags1.Text);
                        row["SpellFamilyFlags2"] = uint.Parse(SpellFamilyFlags2.Text);
                    }
                    row["MaximumAffectedTargets"] = uint.Parse(MaxTargets.Text);
                    row["DamageClass"] = (uint)SpellDamageType.SelectedIndex;
                    row["PreventionType"] = (uint)PreventionType.SelectedIndex;
                    row["EffectDamageMultiplier1"] = float.Parse(EffectDamageMultiplier1.Text);
                    row["EffectDamageMultiplier2"] = float.Parse(EffectDamageMultiplier2.Text);
                    row["EffectDamageMultiplier3"] = float.Parse(EffectDamageMultiplier3.Text);

                    uint schoolMask = 0;
                    if (isTbcOrGreater)
                    {
                        schoolMask = (S1.IsChecked.Value ? 0x01 : (uint)0x00) +
                            (S2.IsChecked.Value ? 0x02 : (uint)0x00) +
                            (S3.IsChecked.Value ? 0x04 : (uint)0x00) +
                            (S4.IsChecked.Value ? 0x08 : (uint)0x00) +
                            (S5.IsChecked.Value ? 0x10 : (uint)0x00) +
                            (S6.IsChecked.Value ? 0x20 : (uint)0x00) +
                            (S7.IsChecked.Value ? 0x40 : (uint)0x00);
                    }
                    else
                    {
                        var schoolMaskBoxes = new List<CheckBox>();
                        schoolMaskBoxes.Add(S1);
                        schoolMaskBoxes.Add(S2);
                        schoolMaskBoxes.Add(S3);
                        schoolMaskBoxes.Add(S4);
                        schoolMaskBoxes.Add(S5);
                        schoolMaskBoxes.Add(S6);
                        var checkedMaskCount = schoolMaskBoxes.Where((box) => box.IsChecked.HasValue && box.IsChecked.Value).Count();
                        if (checkedMaskCount > 1)
                        {
                            throw new Exception($"A maximum of 1 damage school can be selected, you have selected { checkedMaskCount }.");
                        }
                        if (checkedMaskCount > 0)
                        {
                            if (S1.IsChecked.HasValue && S1.IsChecked.Value)
                                schoolMask = 1;
                            else if (S2.IsChecked.HasValue && S2.IsChecked.Value)
                                schoolMask = 2;
                            else if (S3.IsChecked.HasValue && S3.IsChecked.Value)
                                schoolMask = 3;
                            else if (S4.IsChecked.HasValue && S4.IsChecked.Value)
                                schoolMask = 4;
                            else if (S5.IsChecked.HasValue && S5.IsChecked.Value)
                                schoolMask = 5;
                            else if (S6.IsChecked.HasValue && S6.IsChecked.Value)
                                schoolMask = 6;
                        }
                    }
                    row["SchoolMask"] = schoolMask;

                    if (isWotlkOrGreater)
                    {
                        row["SpellMissileID"] = uint.Parse(SpellMissileID.Text);
                        row["EffectBonusMultiplier1"] = float.Parse(EffectBonusMultiplier1.Text);
                        row["EffectBonusMultiplier2"] = float.Parse(EffectBonusMultiplier2.Text);
                        row["EffectBonusMultiplier3"] = float.Parse(EffectBonusMultiplier3.Text);
                    }

                    var numColumns = WoWVersionManager.GetInstance().SelectedVersion().NumLocales;
                    ThreadSafeTextBox[] boxes = stringObjectMap.Values.ToArray();
                    for (int i = 0; i < (numColumns > 9 ? 9 : numColumns); ++i)
                        row["SpellName" + i] = boxes[i].Text;
                    for (int i = 0; i < (numColumns > 9 ? 9 : numColumns); ++i)
                        row["SpellRank" + i] = boxes[i + 9].Text;
                    for (int i = 0; i < (numColumns > 9 ? 9 : numColumns); ++i)
                        row["SpellTooltip" + i] = boxes[i + 18].Text;
                    for (int i = 0; i < (numColumns > 9 ? 9 : numColumns); ++i)
                        row["SpellDescription" + i] = boxes[i + 27].Text;
                    // 3.3.5a: This seems to mimic Blizzlike values correctly, though I don't understand it at all.
                    // Discussed on modcraft IRC - these fields are not even read by the client.
                    // The structure used in this program is actually incorrect. All the string columns are
                    //   for different locales apart from the last one which is the flag column. So there are
                    //   not multiple flag columns, hence why we only write to the last one here. The current
                    //   released clients only use 9 locales hence the confusion with the other columns.
                    // Not sure on the correct behaviour in 1.12.1
                    var suffix = isWotlkOrGreater ? "7" : isTbcOrGreater ? "" : "0";
                    row["SpellNameFlag" + suffix] = (uint)(TextFlags.NOT_EMPTY);
                    row["SpellRankFlags" + suffix] = (uint)(TextFlags.NOT_EMPTY);
                    row["SpellToolTipFlags" + suffix] = (uint)(TextFlags.NOT_EMPTY);
                    row["SpellDescriptionFlags" + suffix] = (uint)(TextFlags.NOT_EMPTY);

                    row.EndEdit();
                    adapter.CommitChanges(query, q.GetChanges());

                    ShowFlyoutMessage($"Saved spell {selectedID}.");

                    ReloadSpellListForSpellId(row);
                }
                catch (Exception ex)
                {
                    row.CancelEdit();
                    HandleErrorMessage(ex + "\n\n" + ex.InnerException);
                }
                return;
            }

            if (sender == SaveIcon)
            {
                MetroDialogSettings settings = new MetroDialogSettings
                {
                    AffirmativeButtonText = SafeTryFindResource("Yes"),
                    NegativeButtonText = SafeTryFindResource("No")
                };


                MessageDialogStyle style = MessageDialogStyle.AffirmativeAndNegative;
                MessageDialogResult spellOrActive = await this.ShowMessageAsync(SafeTryFindResource("SpellEditor"), SafeTryFindResource("SaveIcon"), style, settings);

                string column = null;
                if (spellOrActive == MessageDialogResult.Affirmative)
                    column = "SpellIconID";
                else if (spellOrActive == MessageDialogResult.Negative)
                    column = "ActiveIconID";
                adapter.Execute($"UPDATE `{"spell"}` SET `{column}` = '{newIconID}' WHERE `ID` = '{selectedID}'");
                return;
            }

            if (sender == ResetSpellIconID)
            {
                adapter.Execute($"UPDATE `{"spell"}` SET `{"SpellIconID"}` = '{1}' WHERE `ID` = '{selectedID}'");
                return;
            }
            if (sender == ResetActiveIconID)
            {
                adapter.Execute($"UPDATE `{"spell"}` SET `{"ActiveIconID"}` = '{0}' WHERE `ID` = '{selectedID}'");
            }
        }

        private bool ReloadSpellListForSpellId(DataRow row)
        {
            var changedId = uint.Parse(row[0].ToString());
            foreach (var item in SelectSpell.Items)
            {
                var panel = item as StackPanel;
                var text = panel.Children[1] as TextBlock;
                // text block is formatted as: $" { id } - { spellName }"
                if (uint.TryParse(text.Text.Substring(0, text.Text.IndexOf('-')).Trim(), out var id) &&
                    changedId == id)
                {
                    text.Text = $" { id } - { row["SpellName" + (GetLocale() - 1)] }";
                    var image = panel.Children[0] as Image;
                    image.ToolTip = row["SpellIconID"].ToString();
                    image.Visibility = Visibility.Hidden;
                    image.Visibility = Visibility.Visible;
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Utilities
        public void ShowFlyoutMessage(string message)
        {
            Flyout.IsOpen = true;
            FlyoutText.Text = message;
        }

        public static T DeepCopy<T>(T obj)
        {
            BinaryFormatter s = new BinaryFormatter();
            using (MemoryStream ms = new MemoryStream())
            {
                s.Serialize(ms, obj);
                ms.Position = 0;
                T t = (T)s.Deserialize(ms);

                return t;
            }
        }

        private void prepareIconEditor()
        {
            var loadIcons = (SpellIconDBC)DBCManager.GetInstance().FindDbcForBinding("SpellIcon");
            loadIcons.LoadImages(64);
            loadIcons.updateIconSize(64, new Thickness(16, 0, 0, 0));
        }

        private class SpellListQueryWorker : BackgroundWorker
        {
            public readonly IDatabaseAdapter Adapter;
            public readonly Stopwatch Watch;

            public SpellListQueryWorker(IDatabaseAdapter adapter, Stopwatch watch)
            {
                Adapter = adapter;
                Watch = watch;
            }
        }

        public int GetLocale()
        {
            if (storedLocale != -1)
                return storedLocale;

            // Attempt localisation on Death Touch, HACKY
            var aboveClassic = WoWVersionManager.GetInstance().SelectedVersion().Identity > 112;
            var name8 = aboveClassic ? ",`SpellName8` " : "";
            DataRowCollection res = adapter.Query("SELECT `id`,`SpellName0`,`SpellName1`,`SpellName2`,`SpellName3`,`SpellName4`," +
                "`SpellName5`,`SpellName6`,`SpellName7`" + name8 + " FROM `spell` WHERE `ID` = '5'").Rows;
            if (res.Count == 0)
                return -1;
            int locale = 0;
            if (res[0] != null)
            {
                for (int i = 1; i < res[0].Table.Columns.Count; ++i)
                {
                    if (res[0][i].ToString().Length > 3)
                    {
                        locale = i;
                        break;
                    }
                }
            }
            storedLocale = locale;
            return locale;
        }
        #endregion

        #region PopulateSelectSpell
        private int selectSpellContentsCount;
        private int selectSpellContentsIndex;

        private void PopulateSelectSpell()
        {
            var selectSpellWatch = new Stopwatch();
            selectSpellWatch.Start();
            selectSpellContentsIndex = 0;
            selectSpellContentsCount = SelectSpell.Items.Count;
            var worker = new SpellListQueryWorker(adapter, selectSpellWatch) {WorkerReportsProgress = true};
            worker.ProgressChanged += _worker_ProgressChanged;

            FilterSpellNames.IsEnabled = false;

            worker.DoWork += delegate
            {
                if (worker.Adapter == null || !Config.IsInit)
                    return;
                int locale = GetLocale();
                if (locale > 0)
                    locale -= 1;

                spellTable.Rows.Clear();

                const uint pageSize = 5000;
                uint lowerBounds = 0;
                DataRowCollection results = GetSpellNames(lowerBounds, 100, locale);
                lowerBounds += 100;
                // Edge case of empty table after truncating, need to send a event to the handler
                if (results != null && results.Count == 0)
                {
                    worker.ReportProgress(0, results);
                }
                while (results != null && results.Count != 0)
                {
                    worker.ReportProgress(0, results);
                    results = GetSpellNames(lowerBounds, pageSize, locale);
                    lowerBounds += pageSize;
                }

                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() => FilterSpellNames.IsEnabled = true));
            };
            worker.RunWorkerAsync();
            worker.RunWorkerCompleted += (sender, args) =>
            {
                if (!(sender is SpellListQueryWorker spellListQueryWorker))
                    return;

                spellListQueryWorker.Watch.Stop();
                Logger.Info($"Loaded spell selection list contents in {spellListQueryWorker.Watch.ElapsedMilliseconds}ms");
            };
        }

        private void _worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // Ignores spells with a iconId <= 0
            var watch = new Stopwatch();
            watch.Start();
            DataRowCollection collection = (DataRowCollection)e.UserState;
            int rowIndex = 0;
            // Reuse existing UI elements if they exist
            if (selectSpellContentsIndex < selectSpellContentsCount)
            {
                foreach (DataRow row in collection)
                {
                    ++rowIndex;
                    if (selectSpellContentsIndex == selectSpellContentsCount ||
                        selectSpellContentsIndex >= SelectSpell.Items.Count)
                    {
                        break;
                    }

                    if (!(SelectSpell.Items[selectSpellContentsIndex] is StackPanel stackPanel))
                        continue;

                    var image = stackPanel.Children[0] as Image;
                    var textBlock = stackPanel.Children[1] as TextBlock;
                    var spellName = row[1].ToString();
                    textBlock.Text = $" {row[0]} - {spellName}";
                    var iconId = uint.Parse(row[2].ToString());
                    if (iconId <= 0)
                        continue;

                    image.ToolTip = iconId.ToString();
                    ++selectSpellContentsIndex;
                }
            }
            // Spawn any new UI elements required
            var newElements = new List<UIElement>();
            for (; rowIndex < collection.Count; ++rowIndex)
            {
                var row = collection[rowIndex];
                var spellName = row[1].ToString();
                var textBlock = new TextBlock {Text = $" {row[0]} - {spellName}"};
                var image = new Image();
                var iconId = uint.Parse(row[2].ToString());
                //if (iconId > 0)
                //{
                    image.ToolTip = iconId.ToString();
                    image.Width = 32;
                    image.Height = 32;
                    image.Margin = new Thickness(1, 1, 1, 1);
                    image.IsVisibleChanged += isSpellListEntryVisibileChanged;
                    var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    stackPanel.Children.Add(image);
                    stackPanel.Children.Add(textBlock);
                    ++selectSpellContentsIndex;
                //}
                newElements.Add(stackPanel);
            }
            // Replace the item source directly, adding each item will raise a high amount of events
            var src = SelectSpell.ItemsSource;
            var newSrc = new List<object>();
            if (src != null)
            {
                // Don't keep more UI elements than we need
                var enumerator = src.GetEnumerator();
                for (int i = 0; i < selectSpellContentsIndex; ++i)
                {
                    if (!enumerator.MoveNext())
                        break;
                    newSrc.Add(enumerator.Current);
                }
            }

            newSrc.AddRange(newElements);
            SelectSpell.ItemsSource = newSrc;
            watch.Stop();
            Logger.Info($"Worker progress change event took {watch.ElapsedMilliseconds}ms to handle");
        }

        private void isSpellListEntryVisibileChanged(object o, DependencyPropertyChangedEventArgs args)
        {
            var image = o as Image;
            if (!(bool)args.NewValue)
            {
                image.Source = null;
                return;
            }
            if (image.Source != null)
            {
                return;
            }
            var loadIcons = (SpellIconDBC)DBCManager.GetInstance().FindDbcForBinding("SpellIcon");
            var iconId = uint.Parse(image.ToolTip.ToString());
            var filePath = loadIcons.GetIconPath(iconId) + ".blp";
            image.Source = BlpManager.GetInstance().GetImageSourceFromBlpPath(filePath);
        }

        private DataRowCollection GetSpellNames(uint lowerBound, uint pageSize, int locale)
        {
            DataTable newSpellNames = adapter.Query(string.Format(@"SELECT `id`,`SpellName{1}`,`SpellIconID` FROM `{0}` ORDER BY `id` LIMIT {2}, {3}",
                 "spell", locale, lowerBound, pageSize));

            spellTable.Merge(newSpellNames, false, MissingSchemaAction.Add);

            return newSpellNames.Rows;
        }
        #endregion

        #region NewIconClick & UpdateMainWindow
        private async void NewIconClick(object sender, RoutedEventArgs e)
        {
            if (adapter == null) { return; }

            MetroDialogSettings settings = new MetroDialogSettings
            {
                AffirmativeButtonText = SafeTryFindResource("SpellIconID"),
                NegativeButtonText = SafeTryFindResource("ActiveIconID")
            };

            const MessageDialogStyle style = MessageDialogStyle.AffirmativeAndNegative;
            MessageDialogResult spellOrActive = await this.ShowMessageAsync(SafeTryFindResource("SpellEditor"), SafeTryFindResource("String4"), style, settings);

            string column = null;
            if (spellOrActive == MessageDialogResult.Affirmative)
                column = "SpellIconID";
            else if (spellOrActive == MessageDialogResult.Negative)
                column = "ActiveIconID";
            adapter.Execute($"UPDATE `{"spell"}` SET `{column}` = '{newIconID}' WHERE `ID` = '{selectedID}'");
        }

        private async void UpdateMainWindow()
        {
            ProgressDialogController controller = null;
            try
            {
                updating = true;

                loadSpell(LoadSpellReporter);

                updating = false;
            }

            catch (Exception ex)
            {
                updating = false;
                if (controller != null)
                    await controller.CloseAsync();
                HandleErrorMessage(ex.Message);
            }
        }

        public void LoadSpellReporter(string value)
        {
            Logger.Debug("[Spell Load] " + value);
        }
        #endregion

        #region SpellStringParsing
        /**
         * Very slow debug method for parsing all spell descriptions and tooltips in enUS/enGB locale and
         * writing the ones that failed to parse.
         */
        private void DebugFuncWriteAllUnparsedStrings()
        {
            DataRowCollection rowResult = adapter.Query(string.Format("SELECT SpellDescription0 || SpellTooltip0 FROM `spell`", selectedID)).Rows;
            if (rowResult == null || rowResult.Count == 0)
                throw new Exception("An error occurred trying to select spell ID: " + selectedID);
            var unparsedStrings = new List<string>();
            foreach (DataRow row in rowResult)
            {
                var str = row[0].ToString();
                var parsedStr = SpellStringParser.ParseString(str, row, this);
                if (parsedStr.Contains("$"))
                {
                    unparsedStrings.Add(str + " | " + parsedStr);
                }
            }
            File.WriteAllLines("debug_unparsed_strings.txt", unparsedStrings);
        }

        private void SpellDescriptionGen_TextChanged(object sender, TextChangedEventArgs e) => SpellGenRefresh(sender as ThreadSafeTextBox, 0);
        private void SpellTooltipGen_TextChanged(object sender, TextChangedEventArgs e) => SpellGenRefresh(sender as ThreadSafeTextBox, 1);
        private void SpellGenRefresh(ThreadSafeTextBox sender, int type)
        {
            if (!int.TryParse(sender.Name[sender.Name.Length - 1].ToString(), out int locale))
                return;
            var spell = GetSpellRowById(selectedID);
            var text = SpellStringParser.ParseString(sender.Text, spell, this);
            if (type == 0)
                spellDescGenFields[locale].ThreadSafeText = text;
            else if (type == 1)
                spellTooltipGenFields[locale].ThreadSafeText = text;
        }
        #endregion

        #region VisualMapBuilder
        private void ButtonClickVisualMapBuild(object sender, RoutedEventArgs e)
        {
            if (!WoWVersionManager.IsWotlkOrGreaterSelected)
            {
                ShowFlyoutMessage("This feature is only supported for patch 3.3.5 WOTLK.");
                return;
            }
            var mapBuilder = new VisualMapBuilder
            {
                DbStartingId = uint.Parse(MapBuilderDbStartingId.Text),
                MapId = uint.Parse(MapBuilderMapId.Text),
                TopLeftX = int.Parse(MapBuilderTopLeftCoordX.Text),
                TopLeftY = int.Parse(MapBuilderTopLeftCoordY.Text),
                BottomRightX = int.Parse(MapBuilderBottomRightCoordX.Text),
                BottomRightY = int.Parse(MapBuilderBottomRightCoordY.Text),
                CoordZ = int.Parse(MapBuilderCoordZ.Text),
                CellSize = int.Parse(MapBuilderCellSize.Text),
                CreatureQuery = GetQueryFromTemplateFile("VisualMapBuilder\\creature.sql_template.txt"),
                CreatureDisplayInfoQuery = GetQueryFromTemplateFile("VisualMapBuilder\\creature_model_info.sql_template.txt"),
                CreatureTemplateQuery = GetQueryFromTemplateFile("VisualMapBuilder\\creature_template.sql_template.txt")
            };
            mapBuilder.RunBuild();
            ShowFlyoutMessage("Created Map Builder files successfully in the Export folder.");
        }

        private string GetQueryFromTemplateFile(string path)
        {
            var queryLines = new List<string>();
            foreach (var line in File.ReadAllLines(path))
            {
                if (!line.StartsWith("#"))
                {
                    queryLines.Add(line);
                }
            }
            return string.Join(" ", queryLines);
        }

        private void MapBuilderTemplate_Initialized(object sender, EventArgs e)
        {
            if (sender == MapBuilderCreatureTemplate)
            {
                MapBuilderCreatureTemplate.Text = File.ReadAllText("VisualMapBuilder\\creature.sql_template.txt");
            }
            else if (sender == MapBuilderCreatureModelInfoTemplate)
            {
                MapBuilderCreatureModelInfoTemplate.Text = File.ReadAllText("VisualMapBuilder\\creature_model_info.sql_template.txt");
            }
            else if (sender == MapBuilderCreatureTemplateTemplate)
            {
                MapBuilderCreatureTemplateTemplate.Text = File.ReadAllText("VisualMapBuilder\\creature_template.sql_template.txt");
            }
        }
        #endregion

        #region LoadSpell (load spell god-function)
        private void loadSpell(UpdateTextFunc updateProgress)
        {
            _currentVisualController = null;
            adapter.Updating = true;
            updateProgress("Querying MySQL data...");
            DataRowCollection rowResult = adapter.Query($"SELECT * FROM `spell` WHERE `ID` = '{selectedID}'").Rows;
            if (rowResult == null || rowResult.Count != 1)
                throw new Exception("An error occurred trying to select spell ID: " + selectedID);
            var row = rowResult[0];
            var numColumns = (int)WoWVersionManager.GetInstance().SelectedVersion().NumLocales;
            var isWotlkOrGreater = WoWVersionManager.IsWotlkOrGreaterSelected;
            var isTbcOrGreater = WoWVersionManager.IsTbcOrGreaterSelected;
            try
            {
                updateProgress("Updating text control's...");
                int i;
                var maxColumns = numColumns > spellDescGenFields.Count ? spellDescGenFields.Count : numColumns;
                for (i = 0; i < maxColumns; ++i)
                {
                    spellDescGenFields[i].ThreadSafeText = SpellStringParser.ParseString(row["SpellDescription" + i].ToString(), row, this);
                    spellTooltipGenFields[i].ThreadSafeText = SpellStringParser.ParseString(row["SpellTooltip" + i].ToString(), row, this);
                }
                for (i = 0; i < maxColumns; ++i)
                {
                    stringObjectMap.TryGetValue(i, out var box);
                    box.ThreadSafeText = row[$"SpellName{i}"];
                }
                for (i = 0; i < maxColumns; ++i)
                {
                    stringObjectMap.TryGetValue(i + 9, out var box);
                    box.ThreadSafeText = row[$"SpellRank{i}"];
                }

                for (i = 0; i < maxColumns; ++i)
                {
                    stringObjectMap.TryGetValue(i + 18, out var box);
                    box.ThreadSafeText = row[$"SpellTooltip{i}"];
                }

                for (i = 0; i < maxColumns; ++i)
                {
                    stringObjectMap.TryGetValue(i + 27, out var box);
                    box.ThreadSafeText = row[$"SpellDescription{i}"];
                }

                updateProgress("Updating category & dispel & mechanic...");
                var loadCategories = (SpellCategory)DBCManager.GetInstance().FindDbcForBinding("SpellCategory");
                Category.ThreadSafeIndex = loadCategories.UpdateCategorySelection(uint.Parse(
                    adapter.Query($"SELECT `Category` FROM `{"spell"}` WHERE `ID` = '{selectedID}'").Rows[0][0].ToString()));

                var loadDispels = (SpellDispelType)DBCManager.GetInstance().FindDbcForBinding("SpellDispelType");
                DispelType.ThreadSafeIndex = loadDispels.UpdateDispelSelection(uint.Parse(
                    adapter.Query($"SELECT `Dispel` FROM `{"spell"}` WHERE `ID` = '{selectedID}'").Rows[0][0].ToString()));

                var loadMechanics = (SpellMechanic)DBCManager.GetInstance().FindDbcForBinding("SpellMechanic");
                MechanicType.SelectedIndex = loadMechanics.UpdateMechanicSelection(uint.Parse(
                    adapter.Query($"SELECT `Mechanic` FROM `{"spell"}` WHERE `ID` = '{selectedID}'").Rows[0][0].ToString()));

                updateProgress("Updating attributes...");
                uint mask = uint.Parse(row["Attributes"].ToString());
                uint flagg = 1;

                foreach (ThreadSafeCheckBox attribute0 in attributes0)
                {
                    attribute0.ThreadSafeChecked = ((mask & flagg) != 0);
                    flagg += flagg;
                }

                mask = uint.Parse(row["AttributesEx"].ToString());
                flagg = 1;

                foreach (ThreadSafeCheckBox attribute1 in attributes1)
                {
                    attribute1.ThreadSafeChecked = ((mask & flagg) != 0);
                    flagg += flagg;
                }

                mask = uint.Parse(row["AttributesEx2"].ToString());
                flagg = 1;

                foreach (ThreadSafeCheckBox attribute2 in attributes2)
                {
                    attribute2.ThreadSafeChecked = ((mask & flagg) != 0);
                    flagg += flagg;
                }

                mask = uint.Parse(row["AttributesEx3"].ToString());
                flagg = 1;

                foreach (ThreadSafeCheckBox attribute3 in attributes3)
                {
                    attribute3.ThreadSafeChecked = ((mask & flagg) != 0);
                    flagg += flagg;
                }

                mask = uint.Parse(row["AttributesEx4"].ToString());
                flagg = 1;

                foreach (ThreadSafeCheckBox attribute4 in attributes4)
                {
                    attribute4.ThreadSafeChecked = ((mask & flagg) != 0);
                    flagg += flagg;
                }

                if (isTbcOrGreater)
                {
                    mask = uint.Parse(row["AttributesEx5"].ToString());
                    flagg = 1;

                    foreach (ThreadSafeCheckBox attribute5 in attributes5)
                    {
                        attribute5.ThreadSafeChecked = ((mask & flagg) != 0);
                        flagg += flagg;
                    }

                    mask = uint.Parse(row["AttributesEx6"].ToString());
                    flagg = 1;

                    foreach (ThreadSafeCheckBox attribute6 in attributes6)
                    {
                        attribute6.ThreadSafeChecked = ((mask & flagg) != 0);
                        flagg += flagg;
                    }

                    updateProgress("Updating stances...");
                    mask = uint.Parse(row["Stances"].ToString());
                    if (mask == 0)
                    {
                        stancesBoxes[0].ThreadSafeChecked = true;
                        for (int f = 1; f < stancesBoxes.Count; ++f) { stancesBoxes[f].ThreadSafeChecked = false; }
                    }
                    else
                    {
                        stancesBoxes[0].ThreadSafeChecked = false;
                        uint flag = 1;
                        for (int f = 1; f < stancesBoxes.Count; ++f)
                        {
                            stancesBoxes[f].ThreadSafeChecked = ((mask & flag) != 0);
                            flag += flag;
                        }
                    }
                }
                if (isWotlkOrGreater)
                {
                    mask = uint.Parse(row["AttributesEx7"].ToString());
                    flagg = 1;

                    foreach (ThreadSafeCheckBox attribute7 in attributes7)
                    {
                        attribute7.ThreadSafeChecked = ((mask & flagg) != 0);
                        flagg += flagg;
                    }
                }
                attributes5.ForEach(box => box.IsEnabled = isTbcOrGreater);
                attributes6.ForEach(box => box.IsEnabled = isTbcOrGreater);
                attributes7.ForEach(box => box.IsEnabled = isWotlkOrGreater);
                stancesBoxes.ForEach(box => box.IsEnabled = isTbcOrGreater);

                updateProgress("Updating targets...");
                mask = uint.Parse(row["Targets"].ToString());
                if (mask == 0)
                {
                    targetBoxes[0].ThreadSafeChecked = true;
                    for (int f = 1; f < targetBoxes.Count; ++f) { targetBoxes[f].ThreadSafeChecked = false; }
                }
                else
                {
                    targetBoxes[0].ThreadSafeChecked = false;
                    uint flag = 1;
                    for (int f = 1; f < targetBoxes.Count; ++f)
                    {
                        targetBoxes[f].ThreadSafeChecked = ((mask & flag) != 0);
                        flag += flag;
                    }
                }

                mask = uint.Parse(row["TargetCreatureType"].ToString());

                if (mask == 0)
                {
                    targetCreatureTypeBoxes[0].ThreadSafeChecked = true;
                    for (int f = 1; f < targetCreatureTypeBoxes.Count; ++f) { targetCreatureTypeBoxes[f].ThreadSafeChecked = false; }
                }
                else
                {
                    targetCreatureTypeBoxes[0].ThreadSafeChecked = false;
                    uint flag = 1;
                    for (int f = 1; f < targetCreatureTypeBoxes.Count; ++f)
                    {
                        targetCreatureTypeBoxes[f].ThreadSafeChecked = ((mask & flag) != 0);
                        flag += flag;
                    }
                }
                updateProgress("Updating spell focus object selection...");
                var loadFocusObjects = (SpellFocusObject)DBCManager.GetInstance().FindDbcForBinding("SpellFocusObject");
                RequiresSpellFocus.ThreadSafeIndex = loadFocusObjects.UpdateSpellFocusObjectSelection(uint.Parse(
                    adapter.Query($"SELECT `RequiresSpellFocus` FROM `{"spell"}` WHERE `ID` = '{selectedID}'").Rows[0][0].ToString()));

                if (isTbcOrGreater)
                {
                    mask = uint.Parse(row["FacingCasterFlags"].ToString());
                    FacingFrontFlag.ThreadSafeChecked = ((mask & 0x1) != 0);
                }
                FacingFrontFlag.IsEnabled = isTbcOrGreater;

                updateProgress("Updating caster aura state...");
                switch (uint.Parse(row["CasterAuraState"].ToString()))
                {
                    case 0: // None
                        CasterAuraState.ThreadSafeIndex = 0;
                        break;
                    case 1: // Defense
                        CasterAuraState.ThreadSafeIndex = 1;
                        break;
                    case 2: // Healthless 20%
                        CasterAuraState.ThreadSafeIndex = 2;
                        break;
                    case 3: // Berserking
                        CasterAuraState.ThreadSafeIndex = 3;
                        break;
                    case 5: // Judgement
                        CasterAuraState.ThreadSafeIndex = 4;
                        break;
                    case 7: // Hunter Parry
                        CasterAuraState.ThreadSafeIndex = 5;
                        break;
                    case 10: // Victory Rush
                        CasterAuraState.ThreadSafeIndex = 6;
                        break;
                    case 11: // Unknown 1
                        CasterAuraState.ThreadSafeIndex = 7;
                        break;
                    case 13: // Healthless 35%
                        CasterAuraState.ThreadSafeIndex = 8;
                        break;
                    case 17: // Enrage
                        CasterAuraState.ThreadSafeIndex = 9;
                        break;
                    case 22: // Unknown 2
                        CasterAuraState.ThreadSafeIndex = 10;
                        break;
                    case 23: // Health Above 75%
                        CasterAuraState.ThreadSafeIndex = 11;
                        break;
                }

                switch (uint.Parse(row["TargetAuraState"].ToString()))
                {
                    case 0: // None
                        TargetAuraState.ThreadSafeIndex = 0;
                        break;
                    case 2: // Healthless 20%
                        TargetAuraState.ThreadSafeIndex = 1;
                        break;
                    case 3: // Berserking
                        TargetAuraState.ThreadSafeIndex = 2;
                        break;
                    case 13: // Healthless 35%
                        TargetAuraState.ThreadSafeIndex = 3;
                        break;
                    case 14: // Conflagrate
                        TargetAuraState.ThreadSafeIndex = 4;
                        break;
                    case 15: // Swiftmend
                        TargetAuraState.ThreadSafeIndex = 5;
                        break;
                    case 16: // Deadly Poison
                        TargetAuraState.ThreadSafeIndex = 6;
                        break;
                    case 18: // Bleeding
                        TargetAuraState.ThreadSafeIndex = 17;
                        break;
                }
                updateProgress("Updating cast time selection...");
                var loadCastTimes = (SpellCastTimes)DBCManager.GetInstance().FindDbcForBinding("SpellCastTimes");
                CastTime.ThreadSafeIndex = loadCastTimes.UpdateCastTimeSelection(uint.Parse(adapter.Query(
                    $"SELECT `CastingTimeIndex` FROM `{"spell"}` WHERE `ID` = '{selectedID}'").Rows[0][0].ToString()));
                updateProgress("Updating other stuff...");
                RecoveryTime.ThreadSafeText = uint.Parse(row["RecoveryTime"].ToString());
                CategoryRecoveryTime.ThreadSafeText = uint.Parse(row["CategoryRecoveryTime"].ToString());

                mask = uint.Parse(row["InterruptFlags"].ToString());
                if (mask == 0)
                {
                    interrupts1[0].ThreadSafeChecked = true;
                    for (int f = 1; f < interrupts1.Count; ++f) { interrupts1[f].ThreadSafeChecked = false; }
                }
                else
                {
                    interrupts1[0].ThreadSafeChecked = false;
                    uint flag = 1;
                    for (int f = 1; f < interrupts1.Count; ++f)
                    {
                        interrupts1[f].ThreadSafeChecked = ((mask & flag) != 0);

                        flag += flag;
                    }
                }

                mask = uint.Parse(row["AuraInterruptFlags"].ToString());
                if (mask == 0)
                {
                    interrupts2[0].ThreadSafeChecked = true;
                    for (int f = 1; f < interrupts2.Count; ++f) { interrupts2[f].ThreadSafeChecked = false; }
                }
                else
                {
                    interrupts2[0].ThreadSafeChecked = false;
                    uint flag = 1;
                    for (int f = 1; f < interrupts2.Count; ++f)
                    {
                        interrupts2[f].ThreadSafeChecked = ((mask & flag) != 0);
                        flag += flag;
                    }
                }

                mask = uint.Parse(row["ChannelInterruptFlags"].ToString());
                if (mask == 0)
                {
                    interrupts3[0].ThreadSafeChecked = true;
                    for (int f = 1; f < interrupts3.Count; ++f) { interrupts3[f].ThreadSafeChecked = false; }
                }
                else
                {
                    interrupts3[0].ThreadSafeChecked = false;
                    uint flag = 1;
                    for (int f = 1; f < interrupts3.Count; ++f)
                    {
                        interrupts3[f].ThreadSafeChecked = ((mask & flag) != 0);
                        flag += flag;
                    }
                }

                mask = uint.Parse(row["ProcFlags"].ToString());
                if (mask == 0)
                {
                    procBoxes[0].ThreadSafeChecked = true;
                    for (int f = 1; f < procBoxes.Count; ++f) { procBoxes[f].ThreadSafeChecked = false; }
                }
                else
                {
                    procBoxes[0].ThreadSafeChecked = false;
                    uint flag = 1;
                    for (int f = 1; f < procBoxes.Count; ++f)
                    {
                        procBoxes[f].ThreadSafeChecked = ((mask & flag) != 0);
                        flag += flag;
                    }
                }

                ProcChance.ThreadSafeText = uint.Parse(row["ProcChance"].ToString());
                ProcCharges.ThreadSafeText = uint.Parse(row["ProcCharges"].ToString());
                MaximumLevel.ThreadSafeText = uint.Parse(row["MaximumLevel"].ToString());
                BaseLevel.ThreadSafeText = uint.Parse(row["BaseLevel"].ToString());
                SpellLevel.ThreadSafeText = uint.Parse(row["SpellLevel"].ToString());

                var loadDurations = (SpellDuration)DBCManager.GetInstance().FindDbcForBinding("SpellDuration");
                Duration.ThreadSafeIndex = loadDurations.UpdateDurationIndexes(uint.Parse(adapter.Query(
                    $"SELECT `DurationIndex` FROM `{"spell"}` WHERE `ID` = '{selectedID}'").Rows[0][0].ToString()));

                uint powerType = uint.Parse(row["PowerType"].ToString());
                // Manually handle 'Health' power type
                if (powerType == (uint.MaxValue - 1))
                    powerType = 13;
                PowerType.ThreadSafeIndex = powerType;
                PowerCost.ThreadSafeText = uint.Parse(row["ManaCost"].ToString());
                ManaCostPerLevel.ThreadSafeText = uint.Parse(row["ManaCostPerLevel"].ToString());
                ManaCostPerSecond.ThreadSafeText = uint.Parse(row["ManaPerSecond"].ToString());
                PerSecondPerLevel.ThreadSafeText = uint.Parse(row["ManaPerSecondPerLevel"].ToString());
                updateProgress("Updating spell range selection...");
                var loadRanges = (SpellRange)DBCManager.GetInstance().FindDbcForBinding("SpellRange");
                Range.ThreadSafeIndex = loadRanges.UpdateSpellRangeSelection(uint.Parse(adapter.Query(
                    $"SELECT `RangeIndex` FROM `{"spell"}` WHERE `ID` = '{selectedID}'").Rows[0][0].ToString()));

                updateProgress("Updating speed, stacks, totems, reagents...");
                Speed.ThreadSafeText = row["Speed"].ToString();
                Stacks.ThreadSafeText = row["StackAmount"].ToString();
                Totem1.ThreadSafeText = row["Totem1"].ToString();
                Totem2.ThreadSafeText = row["Totem2"].ToString();
                Reagent1.ThreadSafeText = row["Reagent1"].ToString();
                Reagent2.ThreadSafeText = row["Reagent2"].ToString();
                Reagent3.ThreadSafeText = row["Reagent3"].ToString();
                Reagent4.ThreadSafeText = row["Reagent4"].ToString();
                Reagent5.ThreadSafeText = row["Reagent5"].ToString();
                Reagent6.ThreadSafeText = row["Reagent6"].ToString();
                Reagent7.ThreadSafeText = row["Reagent7"].ToString();
                Reagent8.ThreadSafeText = row["Reagent8"].ToString();
                ReagentCount1.ThreadSafeText = row["ReagentCount1"].ToString();
                ReagentCount2.ThreadSafeText = row["ReagentCount2"].ToString();
                ReagentCount3.ThreadSafeText = row["ReagentCount3"].ToString();
                ReagentCount4.ThreadSafeText = row["ReagentCount4"].ToString();
                ReagentCount5.ThreadSafeText = row["ReagentCount5"].ToString();
                ReagentCount6.ThreadSafeText = row["ReagentCount6"].ToString();
                ReagentCount7.ThreadSafeText = row["ReagentCount7"].ToString();
                ReagentCount8.ThreadSafeText = row["ReagentCount8"].ToString();

                updateProgress("Updating item class selection...");
                int ID = int.Parse(adapter.Query(
                    $"SELECT `EquippedItemClass` FROM `{"spell"}` WHERE `ID` = '{selectedID}'").Rows[0][0].ToString());
                if (ID == -1)
                {
                    EquippedItemClass.ThreadSafeIndex = 0;
                    //foreach (ThreadSafeCheckBox box in main.equippedItemInventoryTypeMaskBoxes)
                    //  box.threadSafeChecked = false;
                    Dispatcher.Invoke(DispatcherPriority.Send, TimeSpan.Zero, new Func<object>(()
                        => EquippedItemInventoryTypeGrid.IsEnabled = false));
                }
                else if (ID == 2 || ID == 4)
                {
                    Dispatcher.Invoke(DispatcherPriority.Send, TimeSpan.Zero, new Func<object>(()
                        => EquippedItemInventoryTypeGrid.IsEnabled = true));
                }
                else
                {
                    foreach (ThreadSafeCheckBox box in equippedItemInventoryTypeMaskBoxes)
                        box.ThreadSafeChecked = false;
                    Dispatcher.Invoke(DispatcherPriority.Send, TimeSpan.Zero, new Func<object>(()
                        => EquippedItemInventoryTypeGrid.IsEnabled = false));
                }
                var loadItemClasses = (ItemClass)DBCManager.GetInstance().FindDbcForBinding("ItemClass");
                EquippedItemClass.ThreadSafeIndex = loadItemClasses.UpdateItemClassSelection(ID);

                UpdateItemSubClass(long.Parse(row["EquippedItemClass"].ToString()));

                updateProgress("Updating item subclass mask...");
                int intMask = int.Parse(row["EquippedItemSubClassMask"].ToString());
                if (intMask == 0 || intMask == -1)
                {
                    equippedItemSubClassMaskBoxes[0].ThreadSafeChecked = true;
                    for (int f = 1; f < equippedItemSubClassMaskBoxes.Count; ++f) { equippedItemSubClassMaskBoxes[f].ThreadSafeChecked = false; }
                }
                else
                {
                    equippedItemSubClassMaskBoxes[0].ThreadSafeChecked = false;
                    uint flag = 1;
                    foreach (ThreadSafeCheckBox equippedItemSubClassMaskBox in equippedItemSubClassMaskBoxes)
                    {
                        equippedItemSubClassMaskBox.ThreadSafeChecked = ((intMask & flag) != 0);
                        flag += flag;
                    }
                }

                updateProgress("Updating inventory type...");
                intMask = int.Parse(row["EquippedItemInventoryTypeMask"].ToString());
                if (intMask == 0 || intMask == -1)
                {
                    equippedItemInventoryTypeMaskBoxes[0].ThreadSafeChecked = true;
                    for (int f = 1; f < equippedItemInventoryTypeMaskBoxes.Count; ++f) { equippedItemInventoryTypeMaskBoxes[f].ThreadSafeChecked = false; }
                }
                else
                {
                    equippedItemInventoryTypeMaskBoxes[0].ThreadSafeChecked = false;
                    uint flag = 1;
                    foreach (ThreadSafeCheckBox equippedItemInventoryTypeMaskBox in equippedItemInventoryTypeMaskBoxes)
                    {
                        equippedItemInventoryTypeMaskBox.ThreadSafeChecked = ((intMask & flag) != 0);
                        flag += flag;
                    }
                }
                updateProgress("Updating effects 1-3...");
                SpellEffect1.ThreadSafeIndex = int.Parse(row["Effect1"].ToString());
                SpellEffect2.ThreadSafeIndex = int.Parse(row["Effect2"].ToString());
                SpellEffect3.ThreadSafeIndex = int.Parse(row["Effect3"].ToString());
                DieSides1.ThreadSafeText = row["EffectDieSides1"].ToString();
                DieSides2.ThreadSafeText = row["EffectDieSides2"].ToString();
                DieSides3.ThreadSafeText = row["EffectDieSides3"].ToString();
                BasePointsPerLevel1.ThreadSafeText = row["EffectRealPointsPerLevel1"].ToString();
                BasePointsPerLevel2.ThreadSafeText = row["EffectRealPointsPerLevel2"].ToString();
                BasePointsPerLevel3.ThreadSafeText = row["EffectRealPointsPerLevel3"].ToString();
                BasePoints1.ThreadSafeText = row["EffectBasePoints1"].ToString();
                BasePoints2.ThreadSafeText = row["EffectBasePoints2"].ToString();
                BasePoints3.ThreadSafeText = row["EffectBasePoints3"].ToString();
                Mechanic1.ThreadSafeIndex = int.Parse(row["EffectMechanic1"].ToString());
                Mechanic2.ThreadSafeIndex = int.Parse(row["EffectMechanic2"].ToString());
                Mechanic3.ThreadSafeIndex = int.Parse(row["EffectMechanic3"].ToString());
                TargetA1.ThreadSafeIndex = uint.Parse(row["EffectImplicitTargetA1"].ToString());
                TargetA2.ThreadSafeIndex = uint.Parse(row["EffectImplicitTargetA2"].ToString());
                TargetA3.ThreadSafeIndex = uint.Parse(row["EffectImplicitTargetA3"].ToString());
                TargetB1.ThreadSafeIndex = uint.Parse(row["EffectImplicitTargetB1"].ToString());
                TargetB2.ThreadSafeIndex = uint.Parse(row["EffectImplicitTargetB2"].ToString());
                TargetB3.ThreadSafeIndex = uint.Parse(row["EffectImplicitTargetB3"].ToString());

                updateProgress("Updating radius index...");
                var loadRadiuses = (SpellRadius)DBCManager.GetInstance().FindDbcForBinding("SpellRadius");
                var result = adapter.Query($"SELECT `EffectRadiusIndex1`, `EffectRadiusIndex2`, `EffectRadiusIndex3` FROM `{"spell"}` WHERE `ID` = '{selectedID}'").Rows[0];
                uint[] IDs = { uint.Parse(result[0].ToString()), uint.Parse(result[1].ToString()), uint.Parse(result[2].ToString()) };
                RadiusIndex1.ThreadSafeIndex = loadRadiuses.UpdateRadiusIndexes(IDs[0]);
                RadiusIndex2.ThreadSafeIndex = loadRadiuses.UpdateRadiusIndexes(IDs[1]);
                RadiusIndex3.ThreadSafeIndex = loadRadiuses.UpdateRadiusIndexes(IDs[2]);

                updateProgress("Updating effect 1-3 data...");
                ApplyAuraName1.ThreadSafeIndex = int.Parse(row["EffectApplyAuraName1"].ToString());
                ApplyAuraName2.ThreadSafeIndex = int.Parse(row["EffectApplyAuraName2"].ToString());
                ApplyAuraName3.ThreadSafeIndex = int.Parse(row["EffectApplyAuraName3"].ToString());
                Amplitude1.ThreadSafeText = row["EffectAmplitude1"].ToString();
                Amplitude2.ThreadSafeText = row["EffectAmplitude2"].ToString();
                Amplitude3.ThreadSafeText = row["EffectAmplitude3"].ToString();
                MultipleValue1.ThreadSafeText = row["EffectMultipleValue1"].ToString();
                MultipleValue2.ThreadSafeText = row["EffectMultipleValue2"].ToString();
                MultipleValue3.ThreadSafeText = row["EffectMultipleValue3"].ToString();
                ChainTarget1.ThreadSafeText = row["EffectChainTarget1"].ToString();
                ChainTarget2.ThreadSafeText = row["EffectChainTarget2"].ToString();
                ChainTarget3.ThreadSafeText = row["EffectChainTarget3"].ToString();
                ItemType1.ThreadSafeText = row["EffectItemType1"].ToString();
                ItemType2.ThreadSafeText = row["EffectItemType2"].ToString();
                ItemType3.ThreadSafeText = row["EffectItemType3"].ToString();
                MiscValueA1.ThreadSafeText = row["EffectMiscValue1"].ToString();
                MiscValueA2.ThreadSafeText = row["EffectMiscValue2"].ToString();
                MiscValueA3.ThreadSafeText = row["EffectMiscValue3"].ToString();
                if (isTbcOrGreater)
                {
                    MiscValueB1.ThreadSafeText = row["EffectMiscValueB1"].ToString();
                    MiscValueB2.ThreadSafeText = row["EffectMiscValueB2"].ToString();
                    MiscValueB3.ThreadSafeText = row["EffectMiscValueB3"].ToString();
                }
                MiscValueB1.IsEnabled = isTbcOrGreater;
                MiscValueB2.IsEnabled = isTbcOrGreater;
                MiscValueB3.IsEnabled = isTbcOrGreater;
                TriggerSpell1.ThreadSafeText = row["EffectTriggerSpell1"].ToString();
                TriggerSpell2.ThreadSafeText = row["EffectTriggerSpell2"].ToString();
                TriggerSpell3.ThreadSafeText = row["EffectTriggerSpell3"].ToString();
                PointsPerComboPoint1.ThreadSafeText = row["EffectPointsPerComboPoint1"].ToString();
                PointsPerComboPoint2.ThreadSafeText = row["EffectPointsPerComboPoint2"].ToString();
                PointsPerComboPoint3.ThreadSafeText = row["EffectPointsPerComboPoint3"].ToString();

                if (!isWotlkOrGreater)
                {
                    uint familyName = uint.Parse(row["SpellFamilyName"].ToString());
                    SpellFamilyName.ThreadSafeText = familyName.ToString();
                    SpellFamilyFlags.ThreadSafeText = row["SpellFamilyFlags1"].ToString();
                    SpellFamilyFlags1.ThreadSafeText = row["SpellFamilyFlags2"].ToString();

                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => 
                        spellFamilyClassMaskParser?.UpdateSpellFamilyClassMask(this, familyName, isWotlkOrGreater)));
                }
                else
                {
                    SpellMask11.ThreadSafeText = row["EffectSpellClassMaskA1"].ToString();
                    SpellMask21.ThreadSafeText = row["EffectSpellClassMaskA2"].ToString();
                    SpellMask31.ThreadSafeText = row["EffectSpellClassMaskA3"].ToString();
                    SpellMask12.ThreadSafeText = row["EffectSpellClassMaskB1"].ToString();
                    SpellMask22.ThreadSafeText = row["EffectSpellClassMaskB2"].ToString();
                    SpellMask32.ThreadSafeText = row["EffectSpellClassMaskB3"].ToString();
                    SpellMask13.ThreadSafeText = row["EffectSpellClassMaskC1"].ToString();
                    SpellMask23.ThreadSafeText = row["EffectSpellClassMaskC2"].ToString();
                    SpellMask33.ThreadSafeText = row["EffectSpellClassMaskC3"].ToString();

                    uint familyName = uint.Parse(row["SpellFamilyName"].ToString());
                    SpellFamilyName.ThreadSafeText = familyName.ToString();
                    SpellFamilyFlags.ThreadSafeText = row["SpellFamilyFlags"].ToString();
                    SpellFamilyFlags1.ThreadSafeText = row["SpellFamilyFlags1"].ToString();
                    SpellFamilyFlags2.ThreadSafeText = row["SpellFamilyFlags2"].ToString();

                    UpdateSpellMaskCheckBox(uint.Parse(row["EffectSpellClassMaskA1"].ToString()), SpellMask11);
                    UpdateSpellMaskCheckBox(uint.Parse(row["EffectSpellClassMaskA2"].ToString()), SpellMask21);
                    UpdateSpellMaskCheckBox(uint.Parse(row["EffectSpellClassMaskA3"].ToString()), SpellMask31);
                    UpdateSpellMaskCheckBox(uint.Parse(row["EffectSpellClassMaskB1"].ToString()), SpellMask12);
                    UpdateSpellMaskCheckBox(uint.Parse(row["EffectSpellClassMaskB2"].ToString()), SpellMask22);
                    UpdateSpellMaskCheckBox(uint.Parse(row["EffectSpellClassMaskB3"].ToString()), SpellMask32);
                    UpdateSpellMaskCheckBox(uint.Parse(row["EffectSpellClassMaskC1"].ToString()), SpellMask13);
                    UpdateSpellMaskCheckBox(uint.Parse(row["EffectSpellClassMaskC2"].ToString()), SpellMask23);
                    UpdateSpellMaskCheckBox(uint.Parse(row["EffectSpellClassMaskC3"].ToString()), SpellMask33);

                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => 
                        spellFamilyClassMaskParser.UpdateSpellFamilyClassMask(this, familyName, isWotlkOrGreater)));
                }
                SpellFamilyFlags2.IsEnabled = isWotlkOrGreater;
                ToggleAllSpellMaskCheckBoxes(isWotlkOrGreater);

                SpellVisual1.ThreadSafeText = row["SpellVisual1"].ToString();
                SpellVisual2.ThreadSafeText = row["SpellVisual2"].ToString();

                ManaCostPercent.ThreadSafeText = row["ManaCostPercentage"].ToString();
                StartRecoveryCategory.ThreadSafeText = row["StartRecoveryCategory"].ToString();
                StartRecoveryTime.ThreadSafeText = row["StartRecoveryTime"].ToString();
                MaxTargetsLevel.ThreadSafeText = row["MaximumTargetLevel"].ToString();
                MaxTargets.ThreadSafeText = row["MaximumAffectedTargets"].ToString();
                SpellDamageType.ThreadSafeIndex = int.Parse(row["DamageClass"].ToString());
                PreventionType.ThreadSafeIndex = int.Parse(row["PreventionType"].ToString());
                EffectDamageMultiplier1.ThreadSafeText = row["EffectDamageMultiplier1"].ToString();
                EffectDamageMultiplier2.ThreadSafeText = row["EffectDamageMultiplier2"].ToString();
                EffectDamageMultiplier3.ThreadSafeText = row["EffectDamageMultiplier3"].ToString();

                if (isTbcOrGreater)
                {
                    updateProgress("Updating totem categories & load area groups...");
                    var loadTotemCategories = (TotemCategory)DBCManager.GetInstance().FindDbcForBinding("TotemCategory");
                    result = adapter.Query($"SELECT `TotemCategory1`, `TotemCategory2` FROM `{"spell"}` WHERE `ID` = '{selectedID}'").Rows[0];
                    IDs = new[] { uint.Parse(result[0].ToString()), uint.Parse(result[1].ToString()) };
                    TotemCategory1.ThreadSafeIndex = loadTotemCategories.UpdateTotemCategoriesSelection(IDs[0]);
                    TotemCategory2.ThreadSafeIndex = loadTotemCategories.UpdateTotemCategoriesSelection(IDs[1]);
                }
                TotemCategory1.IsEnabled = isTbcOrGreater;
                TotemCategory2.IsEnabled = isTbcOrGreater;
                if (isWotlkOrGreater)
                {
                    var loadAreaGroups = (AreaGroup)DBCManager.GetInstance().FindDbcForBinding("AreaGroup");
                    AreaGroup.ThreadSafeIndex = loadAreaGroups.UpdateAreaGroupSelection(uint.Parse(adapter.Query(
                        $"SELECT `AreaGroupID` FROM `{"spell"}` WHERE `ID` = '{selectedID}'").Rows[0][0].ToString()));
                }
                AreaGroup.IsEnabled = isWotlkOrGreater;

                updateProgress("Updating school mask...");
                mask = uint.Parse(row["SchoolMask"].ToString());
                if (isTbcOrGreater)
                {
                    S1.ThreadSafeChecked = ((mask & 0x01) != 0);
                    S2.ThreadSafeChecked = ((mask & 0x02) != 0);
                    S3.ThreadSafeChecked = ((mask & 0x04) != 0);
                    S4.ThreadSafeChecked = ((mask & 0x08) != 0);
                    S5.ThreadSafeChecked = ((mask & 0x10) != 0);
                    S6.ThreadSafeChecked = ((mask & 0x20) != 0);
                    S7.ThreadSafeChecked = ((mask & 0x40) != 0);
                }
                else
                {
                    S1.ThreadSafeChecked = mask == 1;
                    S2.ThreadSafeChecked = mask == 2;
                    S3.ThreadSafeChecked = mask == 3;
                    S4.ThreadSafeChecked = mask == 4;
                    S5.ThreadSafeChecked = mask == 5;
                    S6.ThreadSafeChecked = mask == 6;
                    S7.ThreadSafeChecked = mask == 7;
                }


                if (isWotlkOrGreater)
                {
                    updateProgress("Updating rune costs...");
                    var loadRuneCosts = (SpellRuneCost)DBCManager.GetInstance().FindDbcForBinding("SpellRuneCost");
                    RuneCost.ThreadSafeIndex = loadRuneCosts.UpdateSpellRuneCostSelection(uint.Parse(adapter.Query(
                        $"SELECT `RuneCostID` FROM `{"spell"}` WHERE `ID` = '{selectedID}'").Rows[0][0].ToString()));
                }
                RuneCost.IsEnabled = isWotlkOrGreater;

                updateProgress("Updating spell missile & effect bonus multiplier...");
                if (isWotlkOrGreater)
                {
                    EffectBonusMultiplier1.ThreadSafeText = row["EffectBonusMultiplier1"].ToString();
                    EffectBonusMultiplier2.ThreadSafeText = row["EffectBonusMultiplier2"].ToString();
                    EffectBonusMultiplier3.ThreadSafeText = row["EffectBonusMultiplier3"].ToString();
                }
                EffectBonusMultiplier1.IsEnabled = isWotlkOrGreater;
                EffectBonusMultiplier2.IsEnabled = isWotlkOrGreater;
                EffectBonusMultiplier3.IsEnabled = isWotlkOrGreater;
                if (isWotlkOrGreater)
                {
                    SpellMissileID.ThreadSafeText = row["SpellMissileID"].ToString();

                    updateProgress("Updating spell description variables & difficulty selection...");
                    var loadDifficulties = (SpellDifficulty)DBCManager.GetInstance().FindDbcForBinding("SpellDifficulty");
                    var loadDescriptionVariables = (SpellDescriptionVariables)DBCManager.GetInstance().FindDbcForBinding("SpellDescriptionVariables");
                    SpellDescriptionVariables.ThreadSafeIndex = loadDescriptionVariables.UpdateSpellDescriptionVariablesSelection(
                        uint.Parse(adapter.Query($"SELECT `SpellDescriptionVariableID` FROM `{"spell"}` WHERE `ID` = '{selectedID}'").Rows[0][0].ToString()));

                    Difficulty.ThreadSafeIndex = loadDifficulties.UpdateDifficultySelection(uint.Parse(adapter.Query(
                        $"SELECT `SpellDifficultyID` FROM `{"spell"}` WHERE `ID` = '{selectedID}'").Rows[0][0].ToString()));
                }
                SpellMissileID.IsEnabled = isWotlkOrGreater;
                SpellDescriptionVariables.IsEnabled = isWotlkOrGreater;
                Difficulty.IsEnabled = isWotlkOrGreater;
            }
            catch (Exception e)
            {
                HandleErrorMessage(string.Format("{0}\n\n{1}", "", e, e.InnerException));
            }
            adapter.Updating = false;
        }

        #region VisualTab
        private void UpdateSpellVisualTab(uint selectedId, uint selectedKit = 0)
        {
            SelectedVisualPath.Content = selectedId.ToString();
            if (_currentVisualController?.VisualId == selectedId && _currentVisualController?.NextLoadAttachmentId == 0)
            {
                return;
            }
            var attachId = _currentVisualController?.NextLoadAttachmentId;
            var controller = selectedId > 0 ? new VisualController(selectedId, adapter) : null;
            if (attachId.HasValue && attachId.Value > 0)
            {
                controller.NextLoadAttachmentId = attachId.Value;
            }
            _currentVisualController = controller;
            UpdateSpellVisualKitList(controller?.VisualKits, selectedKit);
            UpdatePasteability(controller != null);
        }

        private void UpdatePasteability(bool enablePaste)
        {
            if (VisualSettingsGrid.Children.Count > 0)
            {
                var kitList = VisualSettingsGrid.Children[0] as ListBox;
                UpdateVisualListPasteEnabled(VisualController.GetCopiedKitEntry() != null, 
                    kitList, 
                    VisualSettingsGrid);
                if (VisualEffectsListGrid.Children.Count > 0)
                {
                    var effectList = VisualEffectsListGrid.Children[0] as ListBox;
                    UpdateVisualListPasteEnabled(enablePaste && VisualController.GetCopiedEffectEntry() != null && kitList.Items.Count > 0,
                        effectList,
                        VisualEffectsListGrid);
                }
            }
        }

        private void UpdateSpellVisualKitList(List<IVisualListEntry> kitEntries, uint selectedKit = 0)
        {
            // Reuse the existing ListBox if it already exists
            ListBox scrollList;
            var exists = VisualSettingsGrid.Children.Count == 1 && VisualSettingsGrid.Children[0] is ListBox;
            if (exists)
            {
                scrollList = VisualSettingsGrid.Children[0] as ListBox;
            }
            else
            {
                scrollList = new ListBox()
                {
                    Margin = new Thickness(5)
                };
                scrollList.SelectionChanged += VisualScrollList_SelectionChanged;
                var animationDbc = DBCManager.GetInstance().FindDbcForBinding("AnimationData") as AnimationData;
                StartAnimIdCombo.ItemsSource = ConvertBoxListToLabels(animationDbc.GetAllBoxes());
                AnimationIdCombo.ItemsSource = ConvertBoxListToLabels(animationDbc.GetAllBoxes());
            }
            kitEntries?.ForEach(entry => entry.SetDeleteClickAction(DeleteVisualKitAction));
            kitEntries?.ForEach(entry => entry.SetCopyClickAction(CopyVisualKitAction));
            kitEntries?.ForEach(entry => entry.SetPasteClickAction(PasteVisualKitAction));
            scrollList.ClearValue(ItemsControl.ItemsSourceProperty);
            scrollList.ItemsSource = kitEntries;
            if (kitEntries != null && kitEntries.Count > 0)
            {
                VisualKitListEntry kitEntry = selectedKit > 0 ?
                    kitEntry = kitEntries.Select(kit => kit as VisualKitListEntry)
                        .FirstOrDefault(kit => uint.Parse(kit.KitRecord[0].ToString()) == selectedKit) :
                    kitEntries[0] as VisualKitListEntry;
                if (kitEntry == null)
                {
                    kitEntry = kitEntries[0] as VisualKitListEntry;
                }
                UpdateSpellVisualEditor(kitEntry);
                scrollList.SelectedItem = kitEntry;
            }
            else
            {
                ClearSpellVisualEfectList();
                ClearStaticSpellVisualElements();
            }
            if (!exists)
            {
                VisualSettingsGrid.Children.Add(scrollList);
            }
        }

        private void CopyVisualKitAction(IVisualListEntry selectedEntry)
        {
            var exists = VisualSettingsGrid.Children.Count == 1 && VisualSettingsGrid.Children[0] is ListBox;
            if (!exists)
            {
                return;
            }
            var scrollList = VisualSettingsGrid.Children[0] as ListBox;
            UpdateVisualListPasteEnabled(true, scrollList, VisualSettingsGrid);
        }

        private void PasteVisualKitAction() => PasteVisualKitAction(VisualController.GetCopiedKitEntry());
        private void PasteVisualEffectAction() => PasteVisualEffectAction(VisualController.GetCopiedEffectEntry());

        private void PasteVisualKitAction(IVisualListEntry selectedEntry)
        {
            var exists = VisualSettingsGrid.Children.Count == 1 && VisualSettingsGrid.Children[0] is ListBox;
            if (!exists || selectedEntry == null)
            {
                return;
            }
            var scrollList = VisualSettingsGrid.Children[0] as ListBox;
            var entries = scrollList.ItemsSource as List<IVisualListEntry>;

            var visualIdStr = SpellVisual1.ThreadSafeText?.ToString();
            if (visualIdStr == null || !uint.TryParse(visualIdStr, out uint visualId))
            {
                return;
            }
            var keyResource = WoWVersionManager.GetInstance().LookupKeyResource();
            var itemToPaste = VisualController.GetCopiedKitEntry();
            var pasteEntry = new VisualPasteListEntry(itemToPaste, 
                _currentVisualController?.GetAvailableFields(itemToPaste) ?? keyResource.KitColumnKeys.ToList());
            pasteEntry.SetDeleteClickAction(entry =>
            {
                entries = scrollList.ItemsSource as List<IVisualListEntry>;
                entries.Remove(pasteEntry);
                scrollList.ClearValue(ItemsControl.ItemsSourceProperty);
                scrollList.ItemsSource = entries;
            });
            pasteEntry.SetPasteClickAction(entry =>
            {
                var key = pasteEntry.SelectedKey();
                if (key == null || key.Length == 0)
                {
                    return;
                }
                var idToCopy = itemToPaste.KitRecord[0].ToString();
                var visualQuery = visualId > 0 ? "SELECT * FROM spellvisual WHERE id = " + visualId : null;
                var visualResults = visualId > 0 ? adapter.Query(visualQuery) : null;
                var kitResults = adapter.Query("SELECT * FROM spellvisualkit WHERE id = " + idToCopy);
                var newKitId = uint.Parse(adapter.Query("SELECT max(id) FROM spellvisualkit").Rows[0][0].ToString()) + 1;
                if (kitResults.Rows.Count == 0 || (visualResults != null && visualResults.Rows.Count == 0))
                {
                    return;
                }
                // Add new spellvisualkit
                var copyRow = kitResults.Rows[0];
                copyRow[0] = newKitId.ToString();
                adapter.Execute($"INSERT INTO spellvisualkit VALUES ({ string.Join(", ", copyRow.ItemArray) })");

                // Update existing spell visual to point to new kit
                if (visualId > 0)
                {
                    var updateRow = visualResults.Rows[0];
                    updateRow.BeginEdit();
                    updateRow[key] = newKitId;
                    updateRow.EndEdit();

                    adapter.CommitChanges(visualQuery, visualResults);
                }
                // Create new spell visual and update kit reference and spell record
                else
                {
                    var parentResults = adapter.Query("SELECT * FROM spellvisual WHERE ID = " + itemToPaste.ParentVisualId);
                    if (parentResults == null || parentResults.Rows.Count == 0)
                    {
                        return;
                    }
                    var newVisualId = uint.Parse(adapter.Query("SELECT max(id) FROM spellvisual").Rows[0][0].ToString()) + 1;
                    var copyParent = parentResults.Rows[0];
                    copyParent[0] = newVisualId;
                    foreach (var _key in keyResource.KitColumnKeys)
                    {
                        copyParent[_key] = 0;
                    }
                    copyParent[key] = newKitId;
                    adapter.Execute($"INSERT INTO spellvisual VALUES ({ string.Join(", ", copyParent.ItemArray) })");
                    SpellVisual1.ThreadSafeText = newVisualId.ToString();
                    visualId = newVisualId;
                    Button_Click(SaveSpellChanges, null);
                }
                _currentVisualController = null;
                UpdateSpellVisualTab(visualId);
                UpdateMainWindow();
            });

            if (entries == null)
            {
                entries = new List<IVisualListEntry>();
            }
            entries.Add(pasteEntry);
            scrollList.ClearValue(ItemsControl.ItemsSourceProperty);
            scrollList.ItemsSource = entries;
        }

        private void PasteVisualEffectAction(IVisualListEntry selectedEntry)
        {
            if (!uint.TryParse(SpellVisual1.ThreadSafeText?.ToString(), out var visualId))
            {
                return;
            }
            var exists = VisualEffectsListGrid.Children.Count == 1 && VisualEffectsListGrid.Children[0] is ListBox;
            exists = exists && VisualSettingsGrid.Children.Count == 1 && VisualSettingsGrid.Children[0] is ListBox;
            if (!exists || selectedEntry == null)
            {
                return;
            }
            var scrollList = VisualEffectsListGrid.Children[0] as ListBox;
            var kitList = VisualSettingsGrid.Children[0] as ListBox;
            uint parentKitId;
            if (kitList.SelectedItem == null)
            {
                parentKitId = uint.Parse((kitList.Items[0] as VisualKitListEntry).KitRecord[0].ToString());
            }
            else
            {
                parentKitId = uint.Parse((kitList.SelectedItem as VisualKitListEntry).KitRecord[0].ToString());
            }
            var entries = scrollList.ItemsSource as List<IVisualListEntry>;

            var effectEntry = VisualController.GetCopiedEffectEntry();
            var pasteEntry = new VisualPasteListEntry(effectEntry,
                _currentVisualController?.GetAvailableFields(effectEntry) ?? 
                WoWVersionManager.GetInstance().LookupKeyResource().EffectColumnKeys.ToList());
            pasteEntry.SetDeleteClickAction(entry =>
            {
                entries = scrollList.ItemsSource as List<IVisualListEntry>;
                entries.Remove(pasteEntry);
                scrollList.ClearValue(ItemsControl.ItemsSourceProperty);
                scrollList.ItemsSource = entries;
            });
            pasteEntry.SetPasteClickAction(entry =>
            {
                var key = pasteEntry.SelectedKey();
                if (key == null || key.Length == 0)
                {
                    return;
                }
                var idToCopy = effectEntry.IsAttachment ? 
                                effectEntry.AttachRecord[0].ToString() :
                                effectEntry.EffectRecord[0].ToString();

                var effectId = idToCopy;
                DataRow copyAttachRow = null;
                // Create new attachment if exists on current effect
                if (effectEntry.IsAttachment)
                {
                    // Create new attachment
                    var attachResults = adapter.Query("SELECT * FROM spellvisualkitmodelattach WHERE id = " + idToCopy);
                    var newAttachId = uint.Parse(adapter.Query("SELECT max(id) FROM spellvisualkitmodelattach").Rows[0][0].ToString()) + 1;
                    copyAttachRow = attachResults.Rows[0];

                    effectId = copyAttachRow["SpellVisualEffectNameId"].ToString();

                    copyAttachRow[0] = newAttachId.ToString();
                }
                // Create and persist new effect
                var effectResults = adapter.Query("SELECT * FROM spellvisualeffectname WHERE id = " + effectId);
                var newEffectId = uint.Parse(adapter.Query("SELECT max(id) FROM spellvisualeffectname").Rows[0][0].ToString()) + 1;
                var copyRow = effectResults.Rows[0];
                copyRow[0] = newEffectId.ToString();
                var escapedItems = copyRow.ItemArray.Select(item => "'" + adapter.EscapeString(item.ToString()) + "'");
                adapter.Execute($"INSERT INTO spellvisualeffectname VALUES ({ string.Join(", ", escapedItems) })");
                // Update kit to point to new effect if no attachment
                if (!effectEntry.IsAttachment)
                {
                    adapter.Execute($"UPDATE spellvisualkit SET { key } = { newEffectId } WHERE ID = { effectEntry.ParentKitId }");
                }
                // If attachment update effect with new id and persist
                else
                {
                    copyAttachRow["ParentSpellVisualKitId"] = parentKitId;
                    copyAttachRow["SpellVisualEffectNameId"] = newEffectId;
                    escapedItems = copyAttachRow.ItemArray.Select(item => "\"" + item + "\"");
                    adapter.Execute($"INSERT INTO spellvisualkitmodelattach VALUES ({ string.Join(", ", escapedItems) })");
                }
                _currentVisualController = null;
                UpdateSpellVisualTab(visualId, effectEntry.ParentKitId);
            });

            if (entries == null)
            {
                entries = new List<IVisualListEntry>();
            }
            entries.Add(pasteEntry);
            scrollList.ClearValue(ItemsControl.ItemsSourceProperty);
            scrollList.ItemsSource = entries;
        }

        private void DeleteVisualKitAction(IVisualListEntry entry)
        {
            var exists = VisualSettingsGrid.Children.Count == 1 && VisualSettingsGrid.Children[0] is ListBox;
            if (!exists)
            {
                return;
            }
            var scrollList = VisualSettingsGrid.Children[0] as ListBox;
            var entries = scrollList.ItemsSource as List<IVisualListEntry>;
            entries.Remove(entry);
            scrollList.ClearValue(ItemsControl.ItemsSourceProperty);
            scrollList.ItemsSource = entries;
            if (entries.Count > 0)
            {
                scrollList.SelectedItem = entries[0];
            }
            else
            {
                ClearStaticSpellVisualElements();
                ClearSpellVisualEfectList();
            }
        }

        private void ClearSpellVisualEfectList()
        {
            if (VisualEffectsListGrid.Children.Count == 1 && VisualEffectsListGrid.Children[0] is ListBox scrollList)
            {
                scrollList.ClearValue(ItemsControl.ItemsSourceProperty);
                scrollList.ItemsSource = null;
            }
        }

        private void InitialiseSpellVisualEffectList()
        {
            // Get scroll list if it exists
            ListBox scrollList = null;
            var exists = VisualEffectsListGrid.Children.Count == 1 && VisualEffectsListGrid.Children[0] is ListBox;
            if (exists)
            {
                scrollList = VisualEffectsListGrid.Children[0] as ListBox;
            }
            if (scrollList != null)
            {
                return;
            }
            // Build ScrollList
            scrollList = new ListBox()
            {
                Margin = new Thickness(5)
            };
            scrollList.SelectionChanged += VisualScrollList_SelectionChanged;
            VisualEffectsListGrid.Children.Add(scrollList);
            // Populate animation combo box
            if (VisualAttachmentIdCombo.Items.Count == 0)
            {
                var names = Enum.GetValues(typeof(SpellVisualKitModelAttach.AttachmentPoint))
                    .Cast<SpellVisualKitModelAttach.AttachmentPoint>().ToList();
                var animComboSource = new List<Label>(names.Count);
                names.ForEach((name) => animComboSource.Add(new Label() { Content = $"{ (int)name } - { name.ToString() }" }));
                VisualAttachmentIdCombo.ItemsSource = animComboSource;
            }
        }

        private void VisualScrollList_SelectionChanged(object sender, SelectionChangedEventArgs args)
        {
            var added_items = args.AddedItems;
            var listBox = sender as ListBox;
            if (added_items.Count == 1)
            {
                if (listBox.SelectedItem is VisualKitListEntry)
                {
                    if (_currentVisualController != null && _currentVisualController.CancelNextLoad)
                    {
                        _currentVisualController.CancelNextLoad = false;
                        return;
                    }
                    UpdateSpellVisualEditor(listBox.SelectedItem as VisualKitListEntry);
                }
                else if (listBox.SelectedItem is VisualEffectListEntry)
                {
                    UpdateSpellEffectEditor(listBox.SelectedItem as VisualEffectListEntry);
                }
            }
            else
            {
                listBox.UnselectAll();
            }
        }

        private void UpdateSpellVisualEditor(VisualKitListEntry entry)
        {
            var animationDbc = DBCManager.GetInstance().FindDbcForBinding("AnimationData") as AnimationData;
            var record = entry.KitRecord;

            var id = uint.Parse(record["StartAnimId"].ToString());
            var matchRecord = animationDbc.Lookups.SingleOrDefault((container) => container.ID == id);
            int index = 0;
            if (matchRecord != null)
            {
                index = matchRecord.ComboBoxIndex;
            }
            StartAnimIdCombo.SelectedIndex = index;
            id = uint.Parse(record["AnimationId"].ToString());
            matchRecord = animationDbc.Lookups.SingleOrDefault((container) => container.ID == id);
            index = 0;
            if (matchRecord != null)
            {
                index = matchRecord.ComboBoxIndex;
            }
            AnimationIdCombo.SelectedIndex = index;
            SpecialEffect1Txt.Text = record["SpecialEffect1"].ToString();
            SpecialEffect2Txt.Text = record["SpecialEffect2"].ToString();
            SpecialEffect3Txt.Text = record["SpecialEffect3"].ToString();
            WorldEffectTxt.Text = record["WorldEffect"].ToString();
            SoundIdTxt.Text = record["SoundID"].ToString();
            ShakeIdTxt.Text = record["ShakeID"].ToString();
            VisualFlagsTxt.Text = record["Flags"].ToString();

            if (VisualEffectsListGrid.Children.Count != 1)
            {
                return;
            }
            var listBox = VisualEffectsListGrid.Children[0] as ListBox;
            if (listBox == null)
            {
                return;
            }
            var effects = entry.GetAllEffectsAndAttachmentsEntries();
            effects?.ForEach((effect) => effect.SetCopyClickAction(CopyVisualEffectAction));
            effects?.ForEach((effect) => effect.SetDeleteClickAction(DeleteVisualEffectAction));
            effects?.ForEach((effect) => effect.SetPasteClickAction(PasteVisualEffectAction));
            listBox.ClearValue(ItemsControl.ItemsSourceProperty);
            listBox.ItemsSource = effects;
            if (listBox.Items.Count > 0)
            {
                var effectIndex = 0;
                var effect = effects[effectIndex] as VisualEffectListEntry;
                var attachmentId = _currentVisualController?.NextLoadAttachmentId;
                if (attachmentId.HasValue && attachmentId.Value > 0)
                {
                    //foreach (var possibleMatch in effects)
                    for (int i = 0; i < effects.Count; ++i)
                    {
                        if (effects[i] is VisualEffectListEntry match && match.IsAttachment)
                        {
                            if (uint.Parse(match.AttachRecord[0].ToString()) == attachmentId)
                            {
                                effect = match;
                                effectIndex = i;
                                _currentVisualController.NextLoadAttachmentId = 0;
                                _currentVisualController.CancelNextLoad = true;
                                break;
                            }
                        }
                    }
                }
                //UpdateSpellEffectEditor(effect);
                listBox.SelectedItem = effects[effectIndex];
            }
            else
            {
                UpdateSpellEffectEditor(null);
            }
        }

        private void CopyVisualEffectAction(IVisualListEntry copiedEntry)
        {
            var exists = VisualEffectsListGrid.Children.Count == 1 && VisualEffectsListGrid.Children[0] is ListBox;
            if (!exists)
            {
                return;
            }
            var scrollList = VisualEffectsListGrid.Children[0] as ListBox;
            UpdateVisualListPasteEnabled(true, scrollList, VisualEffectsListGrid);
        }

        private void UpdateVisualListPasteEnabled(bool enablePaste, ListBox scrollList, Grid parentGrid)
        {
            var entries = scrollList.ItemsSource as List<IVisualListEntry>;
            entries?.Select(entry => entry as StackPanel)
                ?.Select(entry => entry?.ContextMenu as VisualContextMenu)
                ?.ToList()
                ?.ForEach(entry => entry?.SetCanPaste(enablePaste));
            if (parentGrid.ContextMenu is VisualContextMenu menu)
            {
                menu.SetCanPaste(enablePaste);
            }
        }

        private void DeleteVisualEffectAction(IVisualListEntry entry)
        {
            var selectedKit = (entry as VisualEffectListEntry).ParentKitId;
            var parentVisual = (entry as VisualEffectListEntry).ParentVisualId;
            _currentVisualController = null;
            UpdateSpellVisualTab(parentVisual, selectedKit);
        }

        private void UpdateSpellEffectEditor(VisualEffectListEntry entry)
        {
            var record = entry?.EffectRecord;

            VisualEffectNameTxt.Text = entry != null ? record["Name"].ToString() : string.Empty;
            VisualEffectFilePathTxt.Text = entry != null ? record["FilePath"].ToString() : string.Empty;
            VisualEffectAreaEffectSizeTxt.Text = entry != null ? record["AreaEffectSize"].ToString() : string.Empty;
            VisualEffectScaleTxt.Text = entry != null ? record["Scale"].ToString() : string.Empty;
            if (WoWVersionManager.IsWotlkOrGreaterSelected)
            {
                VisualEffectMinAllowedScaleTxt.Text = entry != null ? record["MinAllowedScale"].ToString() : string.Empty;
                VisualEffectMaxAllowedScaleTxt.Text = entry != null ? record["MaxAllowedScale"].ToString() : string.Empty;
                VisualEffectMinAllowedScaleTxt.IsEnabled = true;
                VisualEffectMaxAllowedScaleTxt.IsEnabled = true;
            }
            else
            {
                VisualEffectMinAllowedScaleTxt.IsEnabled = false;
                VisualEffectMaxAllowedScaleTxt.IsEnabled = false;
            }

            var attachRecord = entry?.AttachRecord;
            ToggleVisualAttachments(attachRecord != null);
            if (attachRecord != null)
            {
                VisualAttachmentIdCombo.SelectedIndex = int.Parse(attachRecord["AttachmentId"].ToString());
                VisualAttachmentOffsetXTxt.Text = attachRecord["OffsetX"].ToString();
                VisualAttachmentOffsetYTxt.Text = attachRecord["OffsetY"].ToString();
                VisualAttachmentOffsetZTxt.Text = attachRecord["OffsetZ"].ToString();
                VisualAttachmentOffsetYawTxt.Text = attachRecord["Yaw"].ToString();
                VisualAttachmentOffsetPitchTxt.Text = attachRecord["Pitch"].ToString();
                VisualAttachmentOffsetRollTxt.Text = attachRecord["Roll"].ToString();
            }

            NewVisualAttachBtn.IsEnabled = WoWVersionManager.IsWotlkOrGreaterSelected && entry != null && !entry.IsAttachment;
        }

        private void ToggleVisualAttachments(bool enabled)
        {
            VisualAttachmentIdCombo.IsEnabled = enabled;
            VisualAttachmentOffsetXTxt.IsEnabled = enabled;
            VisualAttachmentOffsetYTxt.IsEnabled = enabled;
            VisualAttachmentOffsetZTxt.IsEnabled = enabled;
            VisualAttachmentOffsetYawTxt.IsEnabled = enabled;
            VisualAttachmentOffsetPitchTxt.IsEnabled = enabled;
            VisualAttachmentOffsetRollTxt.IsEnabled = enabled;
            if (!enabled)
            {
                ClearStaticSpellVisualAttachElements();
            }
        }

        private void ClearStaticSpellVisualElements()
        {
            SpecialEffect1Txt.Text = string.Empty;
            SpecialEffect2Txt.Text = string.Empty;
            SpecialEffect3Txt.Text = string.Empty;
            WorldEffectTxt.Text = string.Empty;
            SoundIdTxt.Text = string.Empty;
            ShakeIdTxt.Text = string.Empty;
            VisualFlagsTxt.Text = string.Empty;
            VisualEffectNameTxt.Text = string.Empty;
            VisualEffectFilePathTxt.Text = string.Empty;
            VisualEffectAreaEffectSizeTxt.Text = string.Empty;
            VisualEffectScaleTxt.Text = string.Empty;
            VisualEffectMinAllowedScaleTxt.Text = string.Empty;
            VisualEffectMaxAllowedScaleTxt.Text = string.Empty;
            ClearStaticSpellVisualAttachElements();
        }

        private void ClearStaticSpellVisualAttachElements()
        {
            VisualAttachmentOffsetXTxt.Text = string.Empty;
            VisualAttachmentOffsetYTxt.Text = string.Empty;
            VisualAttachmentOffsetZTxt.Text = string.Empty;
            VisualAttachmentOffsetYawTxt.Text = string.Empty;
            VisualAttachmentOffsetPitchTxt.Text = string.Empty;
            VisualAttachmentOffsetRollTxt.Text = string.Empty;
        }
        
        private void NewVisualAttachBtn_Click(object sender, RoutedEventArgs e)
        {
            if (VisualEffectsListGrid.Children.Count == 0 || !(VisualEffectsListGrid.Children[0] is ListBox scrollList))
            {
                return;
            }
            var effectEntry = scrollList.SelectedItem as VisualEffectListEntry;
            if (effectEntry == null)
            {
                return;
            }
            var key = effectEntry.EffectName;
            var effectId = effectEntry.EffectRecord[0].ToString();
            var kitId = effectEntry.ParentKitId;
            var attachId = uint.Parse(adapter.Query("SELECT MAX(id) FROM spellvisualkitmodelattach").Rows[0][0].ToString()) + 1;
            adapter.Execute($"UPDATE spellvisualkit SET { key } = 0 WHERE id = { kitId }");
            adapter.Execute($"INSERT INTO spellvisualkitmodelattach (id, ParentSpellVisualKitId, SpellVisualEffectNameId) VALUES ({attachId}, {kitId}, {effectId})");
            
            _currentVisualController.NextLoadAttachmentId = attachId;
            UpdateSpellVisualTab(effectEntry.ParentVisualId, effectEntry.ParentKitId);
        }

        private void SaveVisualChangesBtn_Click(object sender, RoutedEventArgs e)
        {
            // Get selected kit
            var exists = VisualSettingsGrid.Children.Count == 1 && VisualSettingsGrid.Children[0] is ListBox;
            if (!exists)
            {
                return;
            }
            var kitScrollList = VisualSettingsGrid.Children[0] as ListBox;
            var selectedKitItem = kitScrollList.SelectedItem;
            if (selectedKitItem == null)
            {
                return;
            }
            var selectedKit = selectedKitItem as VisualKitListEntry;
            if (selectedKit == null)
            {
                return;
            }
            // Get selected effect/attachment
            if (VisualEffectsListGrid.Children.Count == 0 || !(VisualEffectsListGrid.Children[0] is ListBox effectScrolList))
            {
                return;
            }
            var selectedEffect = effectScrolList.SelectedItem as VisualEffectListEntry;
            // Save values to kit
            var kitId = selectedKit.KitRecord[0].ToString();
            var kitQuery = "SELECT * FROM spellvisualkit WHERE id = " + kitId;
            var kitResults = adapter.Query(kitQuery);
            var kitRecord = kitResults.Rows[0];
            kitRecord.BeginEdit();
            var selectedStartAnim = StartAnimIdCombo.SelectedItem.ToString();
            kitRecord["StartAnimId"] = selectedStartAnim.Length > 0 ? selectedStartAnim.Substring(0, selectedStartAnim.IndexOf(' ')) : "0";
            var selectedAnim = AnimationIdCombo.SelectedItem.ToString();
            kitRecord["AnimationId"] = selectedAnim.Length > 0 ? selectedAnim.Substring(0, selectedAnim.IndexOf(' ')) : "0";
            kitRecord["SpecialEffect1"] = SpecialEffect1Txt.Text;
            kitRecord["SpecialEffect2"] = SpecialEffect2Txt.Text;
            kitRecord["SpecialEffect3"] = SpecialEffect3Txt.Text;
            kitRecord["WorldEffect"] = WorldEffectTxt.Text;
            kitRecord["SoundID"] = SoundIdTxt.Text;
            kitRecord["ShakeID"] = ShakeIdTxt.Text;
            kitRecord["Flags"] = VisualFlagsTxt.Text;
            kitRecord.EndEdit();
            adapter.CommitChanges(kitQuery, kitResults);
            var message = "Saved Kit " + kitId;
            // Save values to effect
            if (selectedEffect == null)
            {
                return;
            }
            var effectId = selectedEffect.IsAttachment ? selectedEffect.AttachRecord[0].ToString() : selectedEffect.EffectRecord[0].ToString();
            if (selectedEffect.IsAttachment)
            {
                var attachQuery = "SELECT * FROM spellvisualkitmodelattach WHERE id = " + effectId;
                var attachResults = adapter.Query(attachQuery);
                var attachRecord = attachResults.Rows[0];
                attachRecord.BeginEdit();
                attachRecord["AttachmentId"] = VisualAttachmentIdCombo.SelectedIndex;
                attachRecord["OffsetX"] = VisualAttachmentOffsetXTxt.Text;
                attachRecord["OffsetY"] = VisualAttachmentOffsetYTxt.Text;
                attachRecord["OffsetZ"] = VisualAttachmentOffsetZTxt.Text;
                attachRecord["Yaw"] = VisualAttachmentOffsetYawTxt.Text;
                attachRecord["Pitch"] = VisualAttachmentOffsetPitchTxt.Text;
                attachRecord["Roll"] = VisualAttachmentOffsetRollTxt.Text;
                attachRecord.EndEdit();
                adapter.CommitChanges(attachQuery, attachResults);
                message += ", saved attachment " + effectId;
            }
            else
            {
                var effectQuery = "SELECT * FROM spellvisualeffectname WHERE id = " + effectId;
                var effectResults = adapter.Query(effectQuery);
                var effectRecord = effectResults.Rows[0];
                effectRecord.BeginEdit();
                effectRecord["Name"] = VisualEffectNameTxt.Text;
                effectRecord["FilePath"] = VisualEffectFilePathTxt.Text;
                effectRecord["AreaEffectSize"] = VisualEffectAreaEffectSizeTxt.Text;
                effectRecord["Scale"] = VisualEffectScaleTxt.Text;
                if (WoWVersionManager.IsWotlkOrGreaterSelected)
                {
                    effectRecord["MinAllowedScale"] = VisualEffectMinAllowedScaleTxt.Text;
                    effectRecord["MaxAllowedScale"] = VisualEffectMaxAllowedScaleTxt.Text;
                }
                effectRecord.EndEdit();
                adapter.CommitChanges(effectQuery, effectResults);
                message += ", saved effect " + effectId;
            }
            ShowFlyoutMessage(message);
            _currentVisualController = null;
            UpdateSpellVisualTab(selectedKit.ParentVisualId, uint.Parse(kitId));
        }
        #endregion

        private void UpdateSpellMaskCheckBox(uint mask, ThreadSafeComboBox comBox)
        {
            for (int i = 0; i < 32; i++)
            {
                uint maskPow = (uint)Math.Pow(2, i);

                ThreadSafeCheckBox safeCheckBox = (ThreadSafeCheckBox)comBox.Items.GetItemAt(i);
                
                safeCheckBox.ThreadSafeChecked = false;
                if ((mask & maskPow) != 0)
                    safeCheckBox.ThreadSafeChecked = true;
            }
        }

        private void ToggleAllSpellMaskCheckBoxes(bool enabled)
        {
            SpellMask11.IsEnabled = enabled;
            SpellMask12.IsEnabled = enabled;
            SpellMask13.IsEnabled = enabled;
            SpellMask21.IsEnabled = enabled;
            SpellMask22.IsEnabled = enabled;
            SpellMask23.IsEnabled = enabled;
            SpellMask31.IsEnabled = enabled;
            SpellMask32.IsEnabled = enabled;
            SpellMask33.IsEnabled = enabled;
        }
        #endregion

        #region SelectionChanges
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updating || adapter == null || !Config.IsInit || e.OriginalSource != MainTabControl)
                return;
            var tab = e.AddedItems[0];
            if (tab == IconTab)
            {
                prepareIconEditor();
            }
            else if (tab == VisualTab)
            {
                var idStr = SpellVisual1.Text;
                if (idStr.Length == 0 || !uint.TryParse(idStr, out var id))
                {
                    return;
                }
                UpdateSpellVisualTab(id);
            }
            else if (tab == TalentTab)
            {
                // FIXME(Harry)
                if (!WoWVersionManager.IsWotlkOrGreaterSelected)
                {
                    return;
                }
                if (RacesList.Items.Count == 0)
                {
                    var raceDbc = DBCManager.GetInstance().FindDbcForBinding("ChrRaces", true);
                    if (raceDbc != null)
                    {
                        var boxesList = new List<ThreadSafeCheckBox>();
                        var records = raceDbc.GetAllRecords();
                        for (int i = 0; i < records.Count; ++i)
                        {
                            var record = records.ElementAt(i);
                            var name = raceDbc.LookupStringOffset(uint.Parse(record.ElementAt(13 + GetLocale()).Value.ToString()));
                            var checkbox = new ThreadSafeCheckBox
                            {
                                Content = name
                            };
                            boxesList.Add(checkbox);
                        }
                        RacesList.ItemsSource = boxesList;
                    }
                }
                if (ClassesList.Items.Count == 0)
                {
                    var classDbc = DBCManager.GetInstance().FindDbcForBinding("ChrClasses", true);
                    if (classDbc != null)
                    {
                        var boxesList = new List<ThreadSafeCheckBox>();
                        var records = classDbc.GetAllRecords();
                        for (int i = 0; i < records.Count; ++i)
                        {
                            var record = records.ElementAt(i);
                            var name = classDbc.LookupStringOffset(uint.Parse(record.ElementAt(3 + GetLocale()).Value.ToString()));
                            var checkbox = new ThreadSafeCheckBox
                            {
                                Content = name
                            };
                            boxesList.Add(checkbox);
                        }
                        ClassesList.ItemsSource = boxesList;
                    }
                }
            }
        }

        private async void SelectSpell_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var added_items = e.AddedItems;
            if (added_items.Count > 1)
            {
                await this.ShowMessageAsync(SafeTryFindResource("SpellEditor"), SafeTryFindResource("String5"));
                ((ListBox)sender).UnselectAll();
                return;
            }

            if (added_items.Count != 1) 
                return;

            ListBox box = (ListBox)sender;

            StackPanel panel = (StackPanel) box.SelectedItem;
            using (var enumerator = panel.GetChildObjects().GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current is TextBlock block)
                    {
                        string name = block.Text;
                        selectedID = uint.Parse(name.Substring(1, name.IndexOf(' ', 1)));
                        UpdateMainWindow();
                        return;
                    }
                }
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (adapter == null || updating)
                return;
            if (sender == RequiresSpellFocus)
            {
                var loadFocusObjects = (SpellFocusObject)DBCManager.GetInstance().FindDbcForBinding("SpellFocusObject");
                foreach (var dbcBox in loadFocusObjects.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"RequiresSpellFocus"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == AreaGroup)
            {
                var loadAreaGroups = (AreaGroup)DBCManager.GetInstance().FindDbcForBinding("AreaGroup");
                foreach (var dbcBox in loadAreaGroups.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"AreaGroupID"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == Category)
            {
                var loadCategories = (SpellCategory)DBCManager.GetInstance().FindDbcForBinding("SpellCategory");
                foreach (var dbcBox in loadCategories.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"Category"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == DispelType)
            {
                var loadDispels = (SpellDispelType)DBCManager.GetInstance().FindDbcForBinding("SpellDispelType");
                foreach (var dbcBox in loadDispels.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"Dispel"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == MechanicType)
            {
                var loadMechanics = (SpellMechanic)DBCManager.GetInstance().FindDbcForBinding("SpellMechanic");
                foreach (var dbcBox in loadMechanics.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"Mechanic"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == CastTime)
            {
                var loadCastTimes = (SpellCastTimes)DBCManager.GetInstance().FindDbcForBinding("SpellCastTimes");
                foreach (var dbcBox in loadCastTimes.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"CastingTimeIndex"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == Duration)
            {
                var loadDurations = (SpellDuration)DBCManager.GetInstance().FindDbcForBinding("SpellDuration");
                foreach (var dbcBox in loadDurations.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"DurationIndex"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == Difficulty)
            {
                var loadDifficulties = (SpellDifficulty)DBCManager.GetInstance().FindDbcForBinding("SpellDifficulty");
                foreach (var dbcBox in loadDifficulties.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"SpellDifficultyID"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == Range)
            {
                var loadRanges = (SpellRange)DBCManager.GetInstance().FindDbcForBinding("SpellRange");
                foreach (var dbcBox in loadRanges.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"RangeIndex"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == RadiusIndex1)
            {
                var loadRadiuses = (SpellRadius)DBCManager.GetInstance().FindDbcForBinding("SpellRadius");
                foreach (var dbcBox in loadRadiuses.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"EffectRadiusIndex1"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == RadiusIndex2)
            {
                var loadRadiuses = (SpellRadius)DBCManager.GetInstance().FindDbcForBinding("SpellRadius");
                foreach (var dbcBox in loadRadiuses.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"EffectRadiusIndex2"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == RadiusIndex3)
            {
                var loadRadiuses = (SpellRadius)DBCManager.GetInstance().FindDbcForBinding("SpellRadius");
                foreach (var dbcBox in loadRadiuses.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"EffectRadiusIndex3"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == EquippedItemClass)
            {
                var loadItemClasses = (ItemClass)DBCManager.GetInstance().FindDbcForBinding("ItemClass");
                long itemSubClass = loadItemClasses.Lookups[EquippedItemClass.SelectedIndex].ID;
                UpdateItemSubClass(itemSubClass);
                foreach (var dbcBox in loadItemClasses.Lookups)
                {
                    if (EquippedItemClass.SelectedIndex == 5 || EquippedItemClass.SelectedIndex == 3)
                    {
                        EquippedItemInventoryTypeGrid.IsEnabled = true;
                    } 
                    else
                    {
                        EquippedItemInventoryTypeGrid.IsEnabled = false;
                    }

                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"EquippedItemClass"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == TotemCategory1)
            {
                var loadTotemCategories = (TotemCategory)DBCManager.GetInstance().FindDbcForBinding("TotemCategory");
                foreach (var dbcBox in loadTotemCategories.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"TotemCategory1"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == TotemCategory2)
            {
                var loadTotemCategories = (TotemCategory)DBCManager.GetInstance().FindDbcForBinding("TotemCategory");
                foreach (var dbcBox in loadTotemCategories.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"TotemCategory2"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == RuneCost)
            {
                var loadRuneCosts = (SpellRuneCost)DBCManager.GetInstance().FindDbcForBinding("SpellRuneCost");
                foreach (var dbcBox in loadRuneCosts.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"RuneCostID"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }

            if (sender == SpellDescriptionVariables)
            {
                var loadDescriptionVariables = (SpellDescriptionVariables)DBCManager.GetInstance().FindDbcForBinding("SpellDescriptionVariables");
                foreach (var dbcBox in loadDescriptionVariables.Lookups)
                {
                    if (dbcBox.ComboBoxIndex == ((ComboBox)sender).SelectedIndex)
                    {
                        adapter.Execute($"UPDATE `{"spell"}` SET `{"SpellDescriptionVariableID"}` = '{dbcBox.ID}' WHERE `ID` = '{selectedID}'");
                        break;
                    }
                }
            }
        }

        public void UpdateItemSubClass(long classId)
        {
            if (selectedID == 0)
                return;
            if (classId == -1)
            {
                Dispatcher.Invoke(DispatcherPriority.Send, TimeSpan.Zero, new Func<object>(() 
                    => EquippedItemInventoryTypeGrid.IsEnabled = false));

                foreach (ThreadSafeCheckBox box in equippedItemSubClassMaskBoxes)
                {
                    box.ThreadSafeContent = SafeTryFindResource("None");
                    box.ThreadSafeVisibility = Visibility.Hidden;
                    //box.threadSafeEnabled = false;
                }
                return;
            }

            Dispatcher.Invoke(DispatcherPriority.Send, TimeSpan.Zero, new Func<object>(() 
                => EquippedItemSubClassGrid.IsEnabled = true));
            uint num = 0;
            var loadItemSubClasses = (ItemSubClass)DBCManager.GetInstance().FindDbcForBinding("ItemSubClass");
            foreach (ThreadSafeCheckBox box in equippedItemSubClassMaskBoxes)
            {
                ItemSubClass.ItemSubClassLookup itemLookup = loadItemSubClasses.LookupClassAndSubclass(classId, num);
                if (itemLookup.Name != null)
                {
                    box.ThreadSafeContent = itemLookup.Name;
                    //box.threadSafeEnabled = true;
                    box.ThreadSafeVisibility = Visibility.Visible;
                }
                else
                {
                    box.ThreadSafeContent = SafeTryFindResource("None"); ;
                    box.ThreadSafeVisibility = Visibility.Hidden;
                    //box.threadSafeEnabled = false;
                }
                box.ThreadSafeChecked = false;
                num++;
            }
        }

        private void slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IconGrid == null || !IconGrid.IsInitialized)
            {
                return;
            }
            double newSize = e.NewValue / 4;
            var margin = new Thickness(newSize, 0, 0, 0);
            ((SpellIconDBC)DBCManager.GetInstance().FindDbcForBinding("SpellIcon"))?.updateIconSize(newSize, margin);
            foreach (Image image in IconGrid.Children)
            {
                image.Margin = margin;
                image.Width = e.NewValue;
                image.Height = e.NewValue;
            }
        }

        public DataRow GetSpellRowById(uint spellId) => adapter.Query($"SELECT * FROM `{"spell"}` WHERE `ID` = '{spellId}' LIMIT 1").Rows[0];

        public string GetSpellNameById(uint spellId)
        {
            var dr = spellTable.Select($"id = {spellId}");
            return dr.Length == 1 ? dr[0]["SpellName" + (GetLocale() - 1)].ToString() : "";
        }
        #endregion
        private void MultilingualSwitch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string language = e.AddedItems[0].ToString();
            string path = $"pack://SiteOfOrigin:,,,/Languages/{language}.xaml";
            Application.Current.Resources.MergedDictionaries[0].Source = new Uri(path);
            Config.Language = language;
            RefreshAllUIElements();
        }

        private void MultilingualSwitch_Initialized(object sender, EventArgs e)
        {
            string configLanguage = Config.Language;
            configLanguage = configLanguage == "" ? "enUS" : configLanguage;

            MultilingualSwitch.Items.Add("enUS");
            int index = 0;
            foreach (var item in Directory.GetFiles("Languages"))
            {
                FileInfo f = new FileInfo(item);
                if (f.Extension != ".xaml")
                    continue;
                string fileName = new FileInfo(item).Name.Replace(f.Extension, "");
                if (fileName != "enUS")
                    MultilingualSwitch.Items.Add(fileName);

                if (fileName == configLanguage)
                    index = MultilingualSwitch.Items.Count - 1;
            }
            // We want the selection changed event to fire first if the index is > 0
            if (index > 0)
            {
                MultilingualSwitch.SelectionChanged += MultilingualSwitch_SelectionChanged;
            }
            MultilingualSwitch.SelectedIndex = index;
            if (index == 0)
            {
                MultilingualSwitch.SelectionChanged += MultilingualSwitch_SelectionChanged;
            }
        }

        private void FilterSpellEffectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != FilterSpellEffectCombo)
            {
                return;
            }
            var box = sender as ComboBox;
            var selected = box.SelectedItem?.ToString() ?? "0 ";
            var id = int.Parse(selected.Substring(0, selected.IndexOf(' ')));
            var view = CollectionViewSource.GetDefaultView(SelectSpell.Items);
            // Clear filter if id is 0
            if (id == 0)
            {
                view.Filter = obj => true;
                return;
            }
            // Collect all spell ID's with the effect id
            var matchingSpells = adapter.Query($"SELECT id FROM spell WHERE Effect1 = {id} or Effect2 = {id} or Effect3 = {id}").Rows;
            var matchingSpellsSet = new HashSet<string>();
            foreach (DataRow record in matchingSpells)
            {
                matchingSpellsSet.Add(record[0].ToString());
            }
            // Apply filter
            view.Filter = obj =>
            {
                var panel = obj as StackPanel;
                using (var enumerator = panel.GetChildObjects().GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        if (!(enumerator.Current is TextBlock block))
                            continue;
                        var name = block.Text.TrimStart();
                        var blockId = name.Substring(0, name.IndexOf(' '));
                        return matchingSpellsSet.Contains(blockId);
                    }
                }
                return false;
            };
        }

        private void FilterAuraCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != FilterAuraCombo)
            {
                return;
            }
            var box = sender as ComboBox;
            var selected = box.SelectedItem?.ToString() ?? "0 ";
            var id = int.Parse(selected.Substring(0, selected.IndexOf(' ')));
            var view = CollectionViewSource.GetDefaultView(SelectSpell.Items);
            // Clear filter if id is 0
            if (id == 0)
            {
                view.Filter = obj => true;
                return;
            }
            // Collect all spell ID's with the effect id
            var matchingSpells = adapter.Query($"SELECT id FROM spell WHERE EffectApplyAuraName1 = {id} or EffectApplyAuraName2 = {id} or EffectApplyAuraName3 = {id}").Rows;
            var matchingSpellsSet = new HashSet<string>();
            foreach (DataRow record in matchingSpells)
            {
                matchingSpellsSet.Add(record[0].ToString());
            }
            // Apply filter
            view.Filter = obj =>
            {
                var panel = obj as StackPanel;
                using (var enumerator = panel.GetChildObjects().GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        if (!(enumerator.Current is TextBlock block))
                            continue;
                        var name = block.Text.TrimStart();
                        var blockId = name.Substring(0, name.IndexOf(' '));
                        return matchingSpellsSet.Contains(blockId);
                    }
                }
                return false;
            };
        }

        private string SafeTryFindResource(object key)
        {
            var resource = TryFindResource(key);
            return resource != null ? resource.ToString() : $"Language files out of date, missing key: {key}";
        }
    }
}
