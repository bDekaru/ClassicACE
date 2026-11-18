using System;
using System.Linq;

using ACE.DatLoader;
using ACE.Server.Managers;

namespace ACE.Server.Network.GameMessages.Messages
{
    public class GameMessageDDDDataMessage : GameMessage
    {
        public GameMessageDDDDataMessage(uint datFileId, DatDatabaseType datDatabaseType)
            : base(GameMessageOpcode.DDD_DataMessage, GameMessageGroup.DatabaseQueue)
        {
            var datFileType = 0;
            var datFileID = 0;

            switch (datDatabaseType)
            {
                case DatDatabaseType.Portal:

                    datFileType = 0;
                    datFileID = 1;

                    break;

                case DatDatabaseType.Cell:

                    datFileType = 1;
                    datFileID = 2;

                    break;

                case DatDatabaseType.Language:

                    datFileType = 1;
                    datFileID = 3;

                    break;

                case DatDatabaseType.HighRes:

                    datFileType = DDDManager.HiFi_String_As_Int; // HiFi
                    datFileID = 1;

                    break;
            }

            var datFileContents = DDDManager.TryGetDatFileContentsForTransmission(datFileId, datDatabaseType, out var datFile, out var isCompressed);

            if (datFileContents == null)
                throw new InvalidOperationException($"{datDatabaseType} dat does not contain 0x{datFileId:X8}.");

            var fileId = datFile.ObjectId;
            var fileType = datFile.GetFileType(datDatabaseType)
                ?? throw new InvalidOperationException($"{datDatabaseType} dat file 0x{fileId:X8} has no known file type.");

            Writer.Write((uint)datFileType); // DatFileType
            Writer.Write((uint)datFileID); // DatFileID

            Writer.Write((uint)fileType);
            Writer.Write(fileId);

            Writer.Write(datFile.Iteration);

            Writer.Write(isCompressed);

            Writer.Write(3); // version
            Writer.Write(datFileContents.Length + 4); // length + size of this message
            Writer.Write(datFileContents);
        }
    }
}
