﻿using SpellEditor.Sources.DBC;
using System;
using System.Data;
using System.Text.RegularExpressions;

namespace SpellEditor.Sources.SpellStringTools
{
    internal class SpellStringReferenceResolver
    {
        private static string STR_SECONDS = " seconds";
        private static string STR_INFINITE_DUR = "until cancelled";
        private static string STR_HEARTHSTONE_LOC = "(hearthstone location)";

        private struct TOKEN_TO_PARSER
        {
            public string TOKEN;
            public Func<string, DataRow, MainWindow, string> tokenFunc;
        }

        private static TOKEN_TO_PARSER rangeParser = new TOKEN_TO_PARSER()
        {
            TOKEN = "$r",
            tokenFunc = (str, record, mainWindow) =>
            {
                if (str.Contains(rangeParser.TOKEN))
                {
                    var dbc = DBCManager.GetInstance().FindDbcForBinding("SpellRange");
                    if (dbc == null)
                    {
                        Console.WriteLine("Unable to handle $r spell string token, SpellRange.dbc not loaded");
                        return str;
                    }
                    var rangeDbc = (SpellRange)dbc;
                    foreach (var entry in rangeDbc.Lookups)
                    {
                        var rangeIndex = (uint)record["RangeIndex"];
                        if (entry.ID == rangeIndex && entry is SpellRange.SpellRangeBoxContainer)
                        {
                            var container = entry as SpellRange.SpellRangeBoxContainer;
                            return str.Replace(rangeParser.TOKEN, container.RangeString);
                        }
                    }
                }
                return str;
            }
        };

        private static TOKEN_TO_PARSER radiusParser = new TOKEN_TO_PARSER()
        {
            TOKEN = "$a1|$a2|$a3|$a",
            tokenFunc = (str, record, mainWindow) =>
            {
                foreach (var token in radiusParser.TOKEN.Split('|'))
                {
                    if (str.Contains(token))
                    {
                        uint index = 0;
                        if (token.Length == 2)
                        {
                            index = 4;
                        }
                        else
                        {
                            index = uint.Parse(token[2].ToString());
                        }
                        uint radiusVal = 0;
                        if (index == 1)
                        {
                            radiusVal = (uint)record["EffectRadiusIndex1"];  
                        }
                        else if (index == 2)
                        {
                            radiusVal = (uint)record["EffectRadiusIndex2"];
                        }
                        else if (index == 3)
                        {
                            radiusVal = (uint)record["EffectRadiusIndex3"];
                        }
                        else if (index == 4)
                        {
                            Console.WriteLine("Unable to handle $a token in spell string");
                            return str;
                        }
                        var dbc = DBCManager.GetInstance().FindDbcForBinding("SpellRadius");
                        if (dbc == null)
                        {
                            Console.WriteLine("Unable to handle $a token in spell string, SpellRadius dbc not loaded");
                            return str;
                        }
                        var radiusDbc = (SpellRadius)dbc;
                        for (int i = 0; i < radiusDbc.Lookups.Count; ++i)
                        {
                            if (radiusVal == radiusDbc.Lookups[i].ID)
                            {
                                string item = "";
                                if (index == 1)
                                {
                                    item = mainWindow.RadiusIndex1.Items[radiusDbc.Lookups[i].ComboBoxIndex].ToString();
                                }
                                else if (index == 2)
                                {
                                    item = mainWindow.RadiusIndex2.Items[radiusDbc.Lookups[i].ComboBoxIndex].ToString();
                                }
                                else if (index == 3)
                                {
                                    item = mainWindow.RadiusIndex3.Items[radiusDbc.Lookups[i].ComboBoxIndex].ToString();
                                }
                                str = str.Replace(token, item.Contains(" ") ? item.Substring(0, item.IndexOf(" ")) : item);
                            }
                        }
                    }
                }
                return str;
            }
        };

        private static TOKEN_TO_PARSER procChanceParser = new TOKEN_TO_PARSER()
        {
            TOKEN = "$h",
            tokenFunc = (str, record, mainWindos) =>
            {
                if (str.Contains(procChanceParser.TOKEN))
                {
                    str = str.Replace(procChanceParser.TOKEN, record["ProcChance"].ToString());
                }
                return str;
            }
        };

        private static TOKEN_TO_PARSER hearthstoneLocationParser = new TOKEN_TO_PARSER()
        {
            TOKEN = "$z",
            tokenFunc = (str, record,mainWindos) =>
            {
                if (str.Contains(hearthstoneLocationParser.TOKEN))
                {
                    str = str.Replace(hearthstoneLocationParser.TOKEN, STR_HEARTHSTONE_LOC);
                }
                return str;
            }
        };

        private static TOKEN_TO_PARSER maxTargetLevelParser = new TOKEN_TO_PARSER()
        {
            TOKEN = "$v",
            tokenFunc = (str, record,mainWindos) =>
            {
                if (str.Contains(maxTargetLevelParser.TOKEN))
                {
                    str = str.Replace(maxTargetLevelParser.TOKEN, record["MaximumTargetLevel"].ToString());
                }
                return str;
            }
        };

        private static TOKEN_TO_PARSER targetsParser = new TOKEN_TO_PARSER()
        {
            TOKEN = "$x1|$x2|$x3|$x",
            tokenFunc = (str, record,mainWindos) =>
            {
                foreach (var token in targetsParser.TOKEN.Split('|'))
                {
                    if (str.Contains(token))
                    {
                        uint index = 0;
                        if (token.Length == 2)
                        {
                            index = 4;
                        }
                        else
                        {
                            index = uint.Parse(token[2].ToString());
                        }
                        uint targetCount = 0;
                        if (index == 1)
                        {
                            targetCount = (uint)record["EffectChainTarget1"];
                        }
                        else if (index == 2)
                        {
                            targetCount = (uint)record["EffectChainTarget2"];
                        }
                        else if (index == 3)
                        {
                            targetCount = (uint)record["EffectChainTarget3"];
                        }
                        else if (index == 4)
                        {
                            targetCount = (uint)record["EffectChainTarget1"]
                                    + (uint)record["EffectChainTarget2"]
                                    + (uint)record["EffectChainTarget3"];
                        }
                        str = str.Replace(token, targetCount.ToString());
                    }
                }

                MatchCollection _matches = Regex.Matches(str, "\\$([0-9]+)x([1-3])");

                foreach (Match _str in _matches)
                {
                    uint _linkId = UInt32.Parse(_str.Groups[1].Value);
                    uint _index = UInt32.Parse(_str.Groups[2].Value);

                    DataRow _linkRecord = GetRecordById(_linkId, mainWindos);

                    if ((uint)_linkRecord["ID"] != 0)
                    {
                        uint newVal = 0;
                        if (_index == 1)
                        {
                            newVal = (uint)_linkRecord["EffectChainTarget1"];
                        }
                        else if (_index == 2)
                        {
                            newVal = (uint)_linkRecord["EffectChainTarget2"];
                        }
                        else if (_index == 3)
                        {
                            newVal = (uint)_linkRecord["EffectChainTarget3"];
                        }
                        str = str.Replace(_str.ToString(), newVal.ToString());
                    }
                }
                return str;
            }
        };

        private static TOKEN_TO_PARSER summaryDamage = new TOKEN_TO_PARSER()
        {
            TOKEN = "$o1|$o2|$o3|$o",
            tokenFunc = (str, record, mainWindow) =>
            {
            var tokens = summaryDamage.TOKEN.Split('|');
            foreach (var token in tokens)
            {
                    if (str.Contains(token))
                    {
                        uint index = 0;
                        double cooldown = 0;
                        if (token.Length == 2)
                        {
                            index = 4;
                        }
                        else
                        {
                            index = uint.Parse(token[2].ToString());
                        }
                        int damage = 0;
                        if (index == 1)
                        {
                            damage = (int)record["EffectDieSides1"] + (int)record["EffectBasePoints1"];
                            cooldown = (uint)record["EffectAmplitude1"] / 1000;
                        }
                        else if (index == 2)
                        {
                            damage = (int)record["EffectDieSides2"] + (int)record["EffectBasePoints2"];
                            cooldown = (uint)record["EffectAmplitude2"] / 1000;
                        }
                        else if (index == 3)
                        {
                            damage = (int)record["EffectDieSides3"] + (int)record["EffectBasePoints3"];
                            cooldown = (uint)record["EffectAmplitude3"] / 1000;
                        }
                        else if (index == 4)
                        {
                            damage = (int)record["EffectDieSides1"] + (int)record["EffectBasePoints1"] +
                                    (int)record["EffectDieSides2"] + (int)record["EffectBasePoints2"] +
                                    (int)record["EffectDieSides3"] + (int)record["EffectBasePoints3"];
                            cooldown = ((uint)record["EffectAmplitude1"] +
                                        (uint)record["EffectAmplitude2"] +
                                        (uint)record["EffectAmplitude3)"]) / 1000;
                        }
                        var entry = DBCManager.GetInstance().FindDbcForBinding("SpellDuration").LookupRecord((uint)record["DurationIndex"]);
                        if (entry != null)
                        {
                            string newStr;
                            int baseDuration = int.Parse(entry["BaseDuration"].ToString());
                            // Convert duration to seconds
                            if (baseDuration == -1)
                                newStr = STR_INFINITE_DUR;
                            else
                            {
                                var seconds = double.Parse(baseDuration.ToString()) / 1000;
                                var total = damage * (seconds / cooldown);
                                newStr = total.ToString();
                            }
                            str = str.Replace(token, newStr);
                        }
                    }
                }
                return str;
            }
        };

        private static TOKEN_TO_PARSER stacksParser = new TOKEN_TO_PARSER()
        {
            TOKEN = "$n",
            tokenFunc = (str, record,mainWindos) =>
            {
                if (str.Contains(stacksParser.TOKEN))
                {
                    str = str.Replace(stacksParser.TOKEN, record["ProcCharges"].ToString());
                }

                MatchCollection _matches = Regex.Matches(str, "\\$([0-9]+)n");

                foreach (Match _str in _matches)
                {
                    uint _LinkId = uint.Parse(_str.Groups[1].Value);
                    DataRow _linkRecord = GetRecordById(_LinkId, mainWindos);

                    if ((uint)_linkRecord["ID"] != 0)
                    {
                        str = str.Replace(_str.ToString(), _linkRecord["ProcCharges"].ToString());
                    }
                }

                return str;
            }
        };

        private static TOKEN_TO_PARSER periodicTriggerParser = new TOKEN_TO_PARSER()
        {
            TOKEN = "$t1|$t2|$t3|$t",
            tokenFunc = (str, record,mainWindow) =>
            {
                var tokens = periodicTriggerParser.TOKEN.Split('|');
                foreach (var token in tokens)
                {
                    if (str.Contains(token))
                    {
                        uint index = 0;
                        if (token.Length == 2)
                        {
                            index = 4;
                        }
                        else
                        {
                            index = uint.Parse(token[2].ToString());
                        }
                        uint newVal = 0;
                        if (index == 1)
                        {
                            newVal = (uint)record["EffectAmplitude1"];
                        }
                        else if (index == 2)
                        {
                            newVal = (uint)record["EffectAmplitude2"];
                        }
                        else if (index == 3)
                        {
                            newVal = (uint)record["EffectAmplitude3"];
                        }
                        else if (index == 4)
                        {
                            newVal = (uint)record["EffectAmplitude1"] + 
                                    (uint)record["EffectAmplitude2"] + 
                                    (uint)record["EffectAmplitude3"];
                        }
                        var singleVal = Single.Parse(newVal.ToString());
                        str = str.Replace(token, (singleVal / 1000).ToString());
                    }
                }

                MatchCollection _matches = Regex.Matches(str, "\\$([0-9]+)t([1-3])");

                foreach (Match _str in _matches)
                {
                    uint _linkId = uint.Parse(_str.Groups[1].Value);
                    uint _index = uint.Parse(_str.Groups[2].Value);
                    DataRow _linkRecord = GetRecordById(_linkId, mainWindow);

                    if ((uint)_linkRecord["ID"] != 0)
                    {
                        uint newVal = 0;
                        if (_index == 1)
                        {
                            newVal = (uint)_linkRecord["EffectAmplitude1"];
                        }
                        else if (_index == 2)
                        {
                            newVal = (uint)_linkRecord["EffectAmplitude2"];
                        }
                        else if (_index == 3)
                        {
                            newVal = (uint)_linkRecord["EffectAmplitude3"];
                        }
                        var singleVal = float.Parse(newVal.ToString());
                        str = str.Replace(_str.ToString(), (singleVal / 1000).ToString());
                    }
                }
                return str;
            }
        };

        private static TOKEN_TO_PARSER durationParser = new TOKEN_TO_PARSER()
        {
            TOKEN = "$d",
            tokenFunc = (str, record, mainWindow) =>
            {
                if (str.Contains(durationParser.TOKEN))
                {
                    var entry = DBCManager.GetInstance().FindDbcForBinding("SpellDuration").LookupRecord((uint)record["DurationIndex"]);
                    if (entry != null)
                    {
                        string newStr;
                        uint baseDuration = uint.Parse(entry["BaseDuration"].ToString());
                        // Convert duration to seconds
                        if (baseDuration == uint.MaxValue)
                            newStr = STR_INFINITE_DUR;
                        else
                        {
                            var seconds = float.Parse(baseDuration.ToString()) / 1000f;
                            newStr = seconds + STR_SECONDS;
                        }
                        str = str.Replace(durationParser.TOKEN, newStr);
                    }
                }

                //Handling strings similar to "$1510d" (spell:1510)
                MatchCollection _matches = Regex.Matches(str, "\\$([0-9]+)d");

                foreach (Match _str in _matches)
                { 
                    uint _LinkId =  uint.Parse(_str.Groups[1].Value);
                    DataRow _linkRecord = GetRecordById(_LinkId, mainWindow);
                    if ((uint)_linkRecord["ID"] != 0)
                    {
                        var entry = DBCManager.GetInstance().FindDbcForBinding("SpellDuration").LookupRecord((uint)_linkRecord["DurationIndex"]);
                        if (entry != null)
                        {
                            string newStr;
                            int baseDuration = int.Parse(entry["BaseDuration"].ToString());
                            // Convert duration to seconds
                            if (baseDuration == -1)
                                newStr = STR_INFINITE_DUR;
                            else
                            {
                                var seconds = float.Parse(baseDuration.ToString()) / 1000f;
                                newStr = seconds + STR_SECONDS;
                            }
                            str = str.Replace(_str.ToString(), newStr);
                        }
                    }
                }
                return str;
            }
        };

        private static TOKEN_TO_PARSER spellEffectParser = new TOKEN_TO_PARSER()
        {
            TOKEN = "$s1|$s2|$s3|$s",
            tokenFunc = (str, record,mainWindos) =>
            {
                var tokens = spellEffectParser.TOKEN.Split('|');

                foreach (var token in tokens)
                {
                    if (str.Contains(token))
                    {
                        var index = 0;
                        if (token.Length == 2)
                        {
                            index = 4;
                        }
                        else
                        {
                            index = int.Parse(token[2].ToString());
                        }
                        int newVal = 0;
                        if (index >= 1 && index <= 3)
                        {
                            newVal = (int)record["EffectBasePoints" + index] + (int)record["EffectDieSides" + index];
                        }
                        else if (index == 4)
                        {
                            for (int i = 1; i <= 3; ++i)
                            {
                                newVal += (int)record["EffectBasePoints" + i] + (int)record["EffectDieSides" + i];
                            }
                        }
                        if (newVal < 0)
                            newVal *= -1;

                        str = str.Replace(token, newVal.ToString());
                    }
                }

                MatchCollection _matches = Regex.Matches(str, "\\$([0-9]+)s([1-3])");

                foreach (Match _str in _matches)
                {
                    uint _linkId = uint.Parse(_str.Groups[1].Value);
                    uint _index = uint.Parse(_str.Groups[2].Value);

                    DataRow _linkRecord = GetRecordById(_linkId, mainWindos);

                    if ((uint)_linkRecord["ID"] != 0)
                    {
                        int newVal = 0;
                        if (_index >= 1 && _index <= 3)
                        {
                            newVal = (int)record["EffectBasePoints" + _index] + (int)record["EffectDieSides" + _index];
                        }
                        str = str.Replace(_str.ToString(), newVal.ToString());
                    }
                }
                return str;
            }
        };
        
        // "Causes ${$m1+0.15*$SPH+0.15*$AP} to ${$M1+0.15*$SPH+0.15*$AP} Holy damage to an enemy target"
        private static readonly TOKEN_TO_PARSER[] TOKEN_PARSERS = {
            procChanceParser,
            spellEffectParser,
            durationParser,
            stacksParser,
            periodicTriggerParser,
            summaryDamage,
            targetsParser,
            maxTargetLevelParser,
            hearthstoneLocationParser,
            radiusParser,
            rangeParser
        };

        public static string GetParsedForm(string rawString, DataRow record, MainWindow mainWindow)
        {
            // If a token starts with $ and a number, it references that as a spell id
            var match = Regex.Match(rawString, "\\$\\d+");
            if (match.Success)
            {
                if (!uint.TryParse(match.Value.Substring(1), out uint otherId))
                {
                    Console.WriteLine("Failed to parse other spell id: " + rawString);
                    return rawString;
                }
                var otherRecord = SpellDBC.GetRecordById(otherId, mainWindow);
                int offset = match.Index + match.Value.Length;
                bool hasPrefix = rawString.StartsWith("$");
                rawString = rawString.Substring(match.Index + match.Value.Length);
                if (hasPrefix)
                    rawString = "$" + rawString;
                foreach (TOKEN_TO_PARSER parser in TOKEN_PARSERS)
                    rawString = parser.tokenFunc(rawString, otherRecord, mainWindow);
                return rawString;
            }
            foreach (TOKEN_TO_PARSER parser in TOKEN_PARSERS)
                rawString = parser.tokenFunc(rawString, record, mainWindow);
            return rawString;
        }

        public static DataRow GetRecordById(uint spellId, MainWindow mainWindow)
        {
            return SpellDBC.GetRecordById(spellId, mainWindow);
        }
    }
}
