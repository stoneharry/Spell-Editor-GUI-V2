﻿using StormLibSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpellEditor.Sources.Tools.MPQ
{
    class MpqExport
    {
        public void CreateMpqFromDbcFileList(string archiveName, List<string> exportList)
        {
            var archivePath = "Export\\" + archiveName;
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
            using (var archive = MpqArchive.CreateNew(archivePath, MpqArchiveVersion.Version1))
            {
                exportList.ForEach((dbcFile) => archive.AddFileFromDisk(dbcFile, 
                    "DBFilesClient\\" + dbcFile.Substring(dbcFile.IndexOf('\\') + 1)));
            }
        }
    }
}
