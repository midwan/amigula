﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amigula.Domain.DTO;
using Amigula.Domain.Interfaces;

namespace Amigula.Domain.Services
{
    public class EmulatorService
    {
        private readonly IEmulatorRepository _emulatorRepository;

        public EmulatorService(IEmulatorRepository emulatorRepository)
        {
            _emulatorRepository = emulatorRepository;
        }

        public EmulatorDto GetEmulatorPaths()
        {
            var emulatorPaths = _emulatorRepository.GetEmulatorPaths();
            return emulatorPaths;
        }
    }
}
