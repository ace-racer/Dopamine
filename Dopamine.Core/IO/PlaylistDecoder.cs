﻿using Dopamine.Core.Base;
using Dopamine.Core.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Dopamine.Core.IO
{
    public class PlaylistDecoder
    {
        #region DecodePlaylistResult
        public class DecodePlaylistResult
        {
            public OperationResult DecodeResult { get; set; }
            public string PlaylistName { get; set; }
            public List<string> Paths { get; set; }
        }
        #endregion

        #region Public
        public DecodePlaylistResult DecodePlaylist(string fileName)
        {
            OperationResult decodeResult = new OperationResult { Result = false };

            string playlistName = string.Empty;
            List<string> paths = new List<string>();

            if (System.IO.Path.GetExtension(fileName) == FileFormats.M3U)
            {
                decodeResult = this.DecodeM3uPlaylist(fileName, ref playlistName, ref paths);
            }
            else if (System.IO.Path.GetExtension(fileName) == FileFormats.WPL | System.IO.Path.GetExtension(fileName) == FileFormats.ZPL)
            {
                decodeResult = this.DecodeZplPlaylist(fileName, ref playlistName, ref paths);
            }

            return new DecodePlaylistResult
            {
                DecodeResult = decodeResult,
                PlaylistName = playlistName,
                Paths = paths
            };
        }
        #endregion

        #region Private
        private OperationResult DecodeM3uPlaylist(string playlistPath, ref string playlistName, ref List<string> filePaths)
        {
            var op = new OperationResult();

            try
            {
                playlistName = System.IO.Path.GetFileNameWithoutExtension(playlistPath);

                string playlistDirectory = System.IO.Path.GetDirectoryName(playlistPath);

                System.IO.StreamReader sr = System.IO.File.OpenText("" + playlistPath + "");

                string line = sr.ReadLine();

                while (!(line == null))
                {
                    // We don't process empty lines and lines containing comments
                    if (!string.IsNullOrEmpty(line) && !line.StartsWith("#"))
                    {
                        if (FileOperations.IsAbsolutePath(line))
                        {
                            // The line contains the full path.
                            if (System.IO.File.Exists(line))
                            {
                                filePaths.Add(line);
                            }
                        }
                        else
                        {
                            // The line contains a relative path, let's construct the full path by ourselves.
                            string filePath = string.Empty;
                            string parsedLine = line;

                            if (line.StartsWith(@"\"))
                            {
                                // Path starts with "\": add preceeding "." to make it a valid relative path.
                                parsedLine = "." + line;
                            }
                            else
                            {
                                // Normal relative paths: 
                                // ".\songs\tune.mp3""
                                // "..\songs\tune.mp3"
                                // "songs\tune.mp3"
                                // "tune.mp3"
                                parsedLine = line;
                            }

                            filePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(playlistDirectory, parsedLine));

                            if (!string.IsNullOrEmpty(filePath) && FileOperations.IsAbsolutePath(filePath))
                            {
                                filePaths.Add(filePath);
                            }
                        }
                    }

                    line = sr.ReadLine();
                }

                op.Result = true;
            }
            catch (Exception ex)
            {
                op.AddMessage(ex.Message);
                op.Result = false;
            }

            return op;
        }

        private OperationResult DecodeZplPlaylist(string playlistPath, ref string playlistName, ref List<string> filePaths)
        {
            OperationResult op = new OperationResult();

            try
            {
                playlistName = System.IO.Path.GetFileNameWithoutExtension(playlistPath);

                XDocument zplDocument = XDocument.Load(playlistPath);

                // Get the title of the playlist
                var titleElement = (from t in zplDocument.Element("smil").Element("head").Elements("title")
                                    select t).FirstOrDefault();

                if (titleElement != null)
                {
                    // If assigning the title which is fetched from the <title/> element fails,
                    // the filename is used as playlist title.
                    try
                    {
                        playlistName = titleElement.Value;

                    }
                    catch (Exception)
                    {
                        // Swallow
                    }
                }

                // Get the songs
                var mediaElements = from t in zplDocument.Element("smil").Element("body").Element("seq").Elements("media")
                                    select t;

                if (mediaElements != null && mediaElements.Count() > 0)
                {
                    foreach (XElement mediaElement in mediaElements)
                    {
                        string realFilePath = "";

                        var filePathPieces = mediaElement.Attribute("src").Value.Split('\\');

                        // Some parts of the path may contain a fake directory
                        // starting with "-2". We filter this out.
                        foreach (string filePathPiece in filePathPieces)
                        {
                            if (!filePathPiece.StartsWith("-2"))
                            {
                                realFilePath = System.IO.Path.Combine(realFilePath, filePathPiece);

                                // Workaround for missing "\" after drive letter
                                if (realFilePath.EndsWith(":"))
                                {
                                    realFilePath += "\\";
                                }
                            }
                        }

                        if (System.IO.File.Exists(realFilePath))
                        {
                            filePaths.Add(realFilePath);
                        }
                    }
                }

                op.Result = true;
            }
            catch (Exception ex)
            {
                op.AddMessage(ex.Message);
                op.Result = false;
            }

            return op;
        }
        #endregion
    }
}
