﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amigula.Domain.Classes;
using Amigula.Domain.DTO;

namespace Amigula.Domain.Interfaces
{
    public interface IGamesRepository
    {
        IEnumerable<GamesDto> GetGamesList();

        bool FilenameExists(string gameFullPath);
        OperationResult CopyFileInPlace(string screenshot, string destination);
    }
}
