/*
 * program.cs 
 * 
 * replicates the unix RM command
 * 
 *  Date        Author          Description
 *  ====        ======          ===========
 *  06-26-25    Craig           initial implementation
 *  10-21-25    Craig           added -f force attribute removal
 *
 */
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace RmClone
{
    class Program
    {
        static bool recursive = false;
        static bool force = false;
        static bool verbose = false;

        /*
         * Main method
         * 
         * parses command line arguments, expands wildcards, and the deletes files or directories
         * according to the specified options.
         */
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            EnableVirtualTerminal();

            if (args.Length == 0)
            {
                ShowHelp();
                //Console.Error.WriteLine("rm: missing operand");
                return;
            }

            var inputTargets = new List<string>();

            foreach (var arg in args)
            {
                switch (arg)
                {
                    case "-r":
                    case "-R":
                        recursive = true;
                        break;
                    case "-f":
                        force = true;
                        break;
                    case "-v":
                        verbose = true;
                        break;
                    case "-?":
                    case "--help":
                        ShowHelp();
                        return;
                    default:
                        inputTargets.Add(arg);
                        break;
                }
            }

            if (inputTargets.Count == 0)
            {
                ShowHelp();
                return;
            }

            /*
             * get all the targets, and delete them!
             */
            var targets = ExpandArguments(inputTargets);

            foreach (var target in targets)
            {
                try
                {
                    if (File.Exists(target))
                    {
                        if (force)
                        {
                            RemoveFileAttributes(target);
                        }
                        File.Delete(target);
                        if (verbose) PrintSuccess($"removed file: {target}");
                    }
                    else if (Directory.Exists(target))
                    {
                        if (!recursive)
                        {
                            Console.Error.WriteLine($"rm: cannot remove '{target}': Is a directory");
                            continue;
                        }

                        if (force)
                        {
                            RemoveDirectoryAttributes(target);
                        }
                        Directory.Delete(target, true);
                        if (verbose) PrintSuccess($"removed directory: {target}");
                    }
                    else if (!force)
                    {
                        Console.Error.WriteLine($"rm: cannot remove '{target}': No such file or directory");
                    }
                }
                catch (Exception ex)
                {
                    if (!force)
                        PrintError($"rm: cannot remove '{target}': {ex.Message}");
                }
            }

        } /* Main */

        /*
         * RemoveFileAttributes
         * 
         * Removes Hidden, System, and ReadOnly attributes from a file
         * to allow forced deletion with -f option
         */
        static void RemoveFileAttributes(string filePath)
        {
            try
            {
                FileAttributes attributes = File.GetAttributes(filePath);

                // Remove ReadOnly, Hidden, and System attributes
                attributes &= ~FileAttributes.ReadOnly;
                attributes &= ~FileAttributes.Hidden;
                attributes &= ~FileAttributes.System;

                File.SetAttributes(filePath, attributes);
            }
            catch
            {
                // Silently ignore attribute removal failures when using -f
            }
        } /* RemoveFileAttributes */

        /*
         * RemoveDirectoryAttributes
         * 
         * Recursively removes Hidden, System, and ReadOnly attributes from a directory
         * and all its contents to allow forced deletion with -f and -r options
         */
        static void RemoveDirectoryAttributes(string dirPath)
        {
            try
            {
                // Remove attributes from the directory itself
                FileAttributes attributes = File.GetAttributes(dirPath);
                attributes &= ~FileAttributes.ReadOnly;
                attributes &= ~FileAttributes.Hidden;
                attributes &= ~FileAttributes.System;
                File.SetAttributes(dirPath, attributes);

                // Recursively process all files
                foreach (var file in Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories))
                {
                    RemoveFileAttributes(file);
                }

                // Recursively process all subdirectories
                foreach (var subDir in Directory.GetDirectories(dirPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        FileAttributes dirAttributes = File.GetAttributes(subDir);
                        dirAttributes &= ~FileAttributes.ReadOnly;
                        dirAttributes &= ~FileAttributes.Hidden;
                        dirAttributes &= ~FileAttributes.System;
                        File.SetAttributes(subDir, dirAttributes);
                    }
                    catch
                    {
                        // Silently ignore attribute removal failures when using -f
                    }
                }
            }
            catch
            {
                // Silently ignore attribute removal failures when using -f
            }
        } /* RemoveDirectoryAttributes */

        /*
         * ExpandArguments takes a list of arguments and expands any wildcards
         * like *.txt or dir/* to the actual file system entries in the current directory.
         * If no matches are found, it returns the original argument.
         */
        static IEnumerable<string> ExpandArguments(IEnumerable<string> args)
        {
            var cwd = Directory.GetCurrentDirectory();

            foreach (var arg in args)
            {
                if (arg.Contains("*") || arg.Contains("?"))
                {
                    var matches = Directory.GetFileSystemEntries(cwd, arg, SearchOption.TopDirectoryOnly);
                    if (matches.Length > 0)
                    {
                        foreach (var match in matches)
                            yield return match;
                    }
                    else
                    {
                        yield return arg; // nothing matched, pass through (so -f works)
                    }
                }
                else
                {
                    yield return arg;
                }
            }
        } /* ExpandArguments */

        /*
         * ShowHelp 
         * 
         * displays the usage information for the rm command.  
         */
        static void ShowHelp()
        {
            Console.WriteLine(@"
Usage: rm [options] <files or directories...>
Options:
  -r       : recursive delete (required for directories)
  -f       : force (suppress errors and remove read-only/hidden/system attributes)
  -v       : verbose output
  -?       : display this help

Wildcards like *.txt are supported and expanded like Unix shells.
Colors:
  Green = success
  Red   = failure
");
        } /* ShowHelp */


        /*
         * PrintSuccess and PrintError
         * 
         * show results of delete operations in color
         */
        static void PrintSuccess(string message)
        {
            Console.WriteLine($"\x1b[32m{message}\x1b[0m");
        } /* PrintSuccess */

        static void PrintError(string message)
        {
            Console.Error.WriteLine($"\x1b[31m{message}\x1b[0m");
        } /* PrintError */

        /*
         * EnableVirtualTerminal
         * 
         * thunks over to the Windows API to enable virtual terminal processing
         */
        static void EnableVirtualTerminal()
        {
            const int STD_OUTPUT_HANDLE = -11;
            var handle = GetStdHandle(STD_OUTPUT_HANDLE);
            GetConsoleMode(handle, out int mode);
            SetConsoleMode(handle, mode | 0x0004);

        } /* EnableVirtualTerminal */

        [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int nStdHandle);
        [DllImport("kernel32.dll")] static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);
        [DllImport("kernel32.dll")] static extern bool SetConsoleMode(IntPtr hConsoleHandle, int dwMode);
    }
}