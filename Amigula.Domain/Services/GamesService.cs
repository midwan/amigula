﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Text.RegularExpressions;
using Amigula.Domain.Classes;
using Amigula.Domain.DTO;
using Amigula.Domain.Interfaces;

namespace Amigula.Domain.Services
{
    public class GamesService
    {
        private readonly IGamesRepository _gamesRepository;
        
        // TODO The below must be stored in the Settings
        private readonly string _screenshotsPath = @"C:\GameBase\Screenshots";

        public GamesService(IGamesRepository gamesRepository)
        {
            _gamesRepository = gamesRepository;
        }

        public IEnumerable<GamesDto> GetGamesList()
        {
            var gamesDto = _gamesRepository.GetGamesList();
            return gamesDto;
        }

        public GameScreenshotsDto PrepareTitleScreenshot(string gameTitle)
        {
            var result = new GameScreenshotsDto();
            if (string.IsNullOrEmpty(gameTitle)) return result;

            result.GameFolder = DetermineTitleSubfolder(gameTitle);
            result.Title = CleanGameTitle(gameTitle);

            result.Screenshot1 = DetermineTitleScreenshot(result.Title, 1);
            result.Screenshot2 = DetermineTitleScreenshot(result.Title, 2);
            result.Screenshot3 = DetermineTitleScreenshot(result.Title, 3);

            return result;
        }

        /// <summary>
        ///     Remove version information and anything with () or [] from title
        /// </summary>
        /// <param name="gameTitle"></param>
        /// <returns>Cleaned up Title</returns>
        private static string CleanGameTitle(string gameTitle)
        {
            // Remove anything in the title containing () or []
            gameTitle = Regex.Replace(gameTitle, @"[\[(].+?[\])]", "");

            // if there's version information (e.g. v1.2) in the filename remove it as well
            if (Regex.IsMatch(gameTitle, @"\sv(\d{1})"))
            {
                gameTitle = gameTitle.Substring(0,
                    gameTitle.IndexOf(" v",
                        StringComparison
                            .OrdinalIgnoreCase));
            }
            return gameTitle;
        }

        /// <summary>
        ///     Get the first letter of the game title, to get the subfolder from that.
        ///     if the first letter is a number, the subfolder should be set to "0"
        ///     in both scenarios we add 2 backslashes at the end, since this is a path.
        /// </summary>
        /// <param name="gameTitle"></param>
        /// <returns>Game Screenshot Folder</returns>
        private static string DetermineTitleSubfolder(string gameTitle)
        {
            int n;
            if (int.TryParse(gameTitle.Substring(0, 1), out n))
                return "0\\";
            return gameTitle.Substring(0, 1) + "\\";
        }

        /// <summary>
        ///     Replace spaces with underscores, adding ".png" at the end
        /// </summary>
        /// <param name="gameTitle"></param>
        /// <param name="screenshotNumber"></param>
        /// <returns></returns>
        private static string DetermineTitleScreenshot(string gameTitle, int screenshotNumber)
        {
            // Screenshot 1 does not get any numbering,
            // Screenshot 2 gets the suffix _1.png,
            // Screenshot 3 gets the suffix _2.png

            var suffix = ".png";

            if (screenshotNumber == 2) suffix = "_1" + suffix;
            if (screenshotNumber == 3) suffix = "_2" + suffix;

            return Regex.Replace(gameTitle, " $", "")
                .Replace(" ", "_") + suffix;
        }

        /// <summary>
        ///     Prepare the title for using it as a parameter in a URL, replace spaces with "%20".
        /// </summary>
        /// <param name="gameTitle"></param>
        /// <returns></returns>
        public string PrepareTitleUrl(string gameTitle)
        {
            if (string.IsNullOrEmpty(gameTitle)) return "";

            var cleanedGameTitle = CleanGameTitle(gameTitle);

            if (cleanedGameTitle.Length > 0)
                cleanedGameTitle = cleanedGameTitle
                    .TrimEnd(' ')
                    .Replace(" ", "%20");

            return cleanedGameTitle;
        }

        /// <summary>
        ///     Determine if a game is multi-disk from the filename, using certain scenarios
        /// </summary>
        /// <param name="gameFullPath"></param>
        /// <returns>A list of the filenames for the game, multi-disk or single disk</returns>
        public IEnumerable<string> GetGameDisks(string gameFullPath)
        {
            // If the game consists of more than 1 Disk, then the first disk should be passed to WinUAE as usual,
            // but the rest of them should go in the DiskSwapper feature of WinUAE. To do that, the config file must be
            // edited and lines diskimage0-19=<path to filename> must be appended/edited.

            // Checks to be done for possible versions of multi-disk games:
            // 1. <game> Disk1.zip, <game> Disk2.zip etc.
            // 2. <game> Disk01.zip, <game> Disk02.zip etc.
            // 3. <game> (Disk 1 of 2).zip, <game> (Disk 2 of 2).zip etc.
            // 4. <game> (Disk 01 of 11).zip, <game> (Disk 02 of 11).zip etc.
            // 5. <game>-1.zip, <game>-2.zip etc.

            var gameDisksFullPath = new List<string>();

            if (IsMultiDiskPattern1(gameFullPath))
            {
                // case 1. <game> Disk1.zip, <game> Disk2.zip etc.
                gameDisksFullPath = GetDisksFullPath(gameFullPath, 1);
                return gameDisksFullPath;
            }

            if (IsMultiDiskPattern2(gameFullPath))
            {
                // case 2. <game> Disk01.zip, <game> Disk02.zip etc.
                gameDisksFullPath = GetDisksFullPath(gameFullPath, 2);
                return gameDisksFullPath;
            }
            if (IsMultiDiskPattern3(gameFullPath))
            {
                // case 3. <game> (Disk 1 of 2).zip, <game> (Disk 2 of 2).zip etc.
                gameDisksFullPath = GetDisksFullPath(gameFullPath, 3);
                return gameDisksFullPath;
            }
            if (IsMultiDiskPattern4(gameFullPath))
            {
                // case 4. <game> (Disk 01 of 11).zip, <game> (Disk 02 of 11).zip etc.
                gameDisksFullPath = GetDisksFullPath(gameFullPath, 4);
                return gameDisksFullPath;
            }
            if (IsMultiDiskPattern5(gameFullPath))
            {
                // case 5. <game>-1.zip, <game>-2.zip etc.
                gameDisksFullPath = GetDisksFullPath(gameFullPath, 5);
                return gameDisksFullPath;
            }
            // if none of the above matches, assume the game has only one disk
            gameDisksFullPath.Add(gameFullPath);
            return gameDisksFullPath;
        }

        private List<string> GetDisksFullPath(string gameFullPath, int method)
        {
            var diskNumber = 1;
            var gameDisksFullPath = new List<string>();

            if (method == 1)
                do
                {
                    gameDisksFullPath.Add(Regex.Replace(gameFullPath, @"Disk(\d{1})\.", "Disk" + diskNumber + "."));
                    diskNumber++;
                } while (
                    _gamesRepository.FilenameExists(Regex.Replace(gameFullPath, @"Disk(\d{1})\.",
                        "Disk" + diskNumber + ".")));

            if (method == 2)
                do
                {
                    gameDisksFullPath.Add(Regex.Replace(gameFullPath, @"Disk(\d{2})\.",
                        "Disk" + diskNumber.ToString("00") + "."));
                    diskNumber++;
                } while (
                    _gamesRepository.FilenameExists(Regex.Replace(gameFullPath, @"Disk(\d{2})\.",
                        "Disk" + diskNumber.ToString("00") + ".")));
            if (method == 3)
                do
                {
                    gameDisksFullPath.Add(Regex.Replace(gameFullPath, @"Disk\s(\d{1})\sof",
                        "Disk " + diskNumber + " of"));
                    diskNumber++;
                } while (
                    _gamesRepository.FilenameExists(Regex.Replace(gameFullPath, @"Disk\s(\d{1})\sof",
                        "Disk " + diskNumber + " of")));

            if (method == 4)
                do
                {
                    gameDisksFullPath.Add(Regex.Replace(gameFullPath, @"Disk\s(\d{2})\sof",
                        "Disk " + diskNumber.ToString("00") + " of"));
                    diskNumber++;
                } while (
                    _gamesRepository.FilenameExists(Regex.Replace(gameFullPath, @"Disk\s(\d{2})\sof",
                        "Disk " + diskNumber.ToString("00") + " of")));

            if (method == 5)
                do
                {
                    gameDisksFullPath.Add(Regex.Replace(gameFullPath, @"-(\d{1})\.", "-" + diskNumber + "."));
                    diskNumber++;
                } while (
                    _gamesRepository.FilenameExists(Regex.Replace(gameFullPath, @"-(\d{1})\.", "-" + diskNumber + ".")));

            return gameDisksFullPath;
        }

        private static bool IsMultiDiskPattern5(string gameFullPath)
        {
            return Regex.IsMatch(gameFullPath, @"-(\d{1})\....$");
        }

        private static bool IsMultiDiskPattern4(string gameFullPath)
        {
            int n;
            return Regex.IsMatch(gameFullPath, @"Disk\s(\d{2})\sof\s(\d{2})") &&
                   int.TryParse(
                       gameFullPath.Substring(
                           gameFullPath.IndexOf("Disk ", StringComparison.OrdinalIgnoreCase) + 5, 2), out n);
        }

        private static bool IsMultiDiskPattern3(string gameFullPath)
        {
            int n;
            return Regex.IsMatch(gameFullPath, @"Disk\s(\d{1})\sof") &&
                   int.TryParse(
                       gameFullPath.Substring(
                           gameFullPath.IndexOf("Disk ", StringComparison.OrdinalIgnoreCase) + 5, 1), out n);
        }

        private static bool IsMultiDiskPattern2(string gameFullPath)
        {
            int n;
            return Regex.IsMatch(gameFullPath, @"Disk(\d{2})\....$") &&
                   int.TryParse(
                       gameFullPath.Substring(
                           gameFullPath.IndexOf("Disk", StringComparison.OrdinalIgnoreCase) + 4, 2), out n);
        }

        private static bool IsMultiDiskPattern1(string gameFullPath)
        {
            int n;
            return Regex.IsMatch(gameFullPath, @"Disk(\d{1})\....$") &&
                   int.TryParse(
                       gameFullPath.Substring(
                           gameFullPath.IndexOf("Disk", StringComparison.OrdinalIgnoreCase) + 4, 1), out n);
        }

        public OperationResult AddGameScreenshot(string gameTitle, string screenshot)
        {
            var gameSubFolder = DetermineTitleSubfolder(gameTitle);

            var renamedScreenshot = RenameNewScreenshotFilename(gameTitle, screenshot);
            var destination = BuildDestinationPath(gameSubFolder, renamedScreenshot);

            var result = _gamesRepository.CopyFileInPlace(screenshot, destination);

            return result;

            //if (!Directory.Exists(Path.Combine(Settings.Default.ScreenshotsPath, gameSubFolder)))
            //    Directory.CreateDirectory(Path.Combine(Settings.Default.ScreenshotsPath, gameSubFolder));
        }

        private string BuildDestinationPath(string gameSubFolder, string renamedScreenshot)
        {
            var combinedPath = Path.Combine(gameSubFolder, renamedScreenshot);
            return combinedPath;
        }

        private string RenameNewScreenshotFilename(string gameTitle, string screenshot)
        {
            // use gametitle + .png as the screenshot name
            // test if that filename already exists
            // if it does, change the screenshot name to gametitle + _1.png
            // test if that filename already exists
            // if it does, change the screenshot name to gametitle + _2.png
            // test if that filename already exists
            // if it still does, then report an error

            var renamedScreenshot = $"{gameTitle}.png";

            if (ScreenshotFileExists(renamedScreenshot))
            {
                for (int i = 1; i < 3; i++)
                {
                    renamedScreenshot = $"{gameTitle}_{i}.png";
                    if (!ScreenshotFileExists(renamedScreenshot)) break;
                }
            }

            if (ScreenshotFileExists(renamedScreenshot))
            {
                // error! The filename already exists even after attempting to increment the numbering!
            }
            else
            {
                _gamesRepository.RenameFile(screenshot, renamedScreenshot);
            }

            throw new NotImplementedException();
            //if (
            //    !File.Exists(Path.Combine(Settings.Default.ScreenshotsPath,
            //        gameSubFolder + gameTitle.Replace(" ", "_") + ".png")))
            //{
            //    File.Copy(screenshotFilename,
            //        Path.Combine(Settings.Default.ScreenshotsPath,
            //            gameSubFolder + gameTitle.Replace(" ", "_") + ".png"));
            //}
            //else if (
            //    !File.Exists(Path.Combine(Settings.Default.ScreenshotsPath,
            //        gameSubFolder + gameTitle.Replace(" ", "_") + "_1.png")))
            //{
            //    File.Copy(screenshotFilename,
            //        Path.Combine(Settings.Default.ScreenshotsPath,
            //            gameSubFolder + gameTitle.Replace(" ", "_") + "_1.png"));
            //}
            //else if (
            //    !File.Exists(Path.Combine(Settings.Default.ScreenshotsPath,
            //        gameSubFolder + gameTitle.Replace(" ", "_") + "_2.png")))
            //{
            //    File.Copy(screenshotFilename,
            //        Path.Combine(Settings.Default.ScreenshotsPath,
            //            gameSubFolder + gameTitle.Replace(" ", "_") + "_2.png"));
            //}
        }

        private bool ScreenshotFileExists(string filename)
        {
            var titleSubFolder = DetermineTitleSubfolder(filename);
            var fullpath = Path.Combine(_screenshotsPath, titleSubFolder, filename);
            return _gamesRepository.FilenameExists(fullpath);
        }
    }
}