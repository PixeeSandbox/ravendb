﻿using System;
using System.Linq;
using System.Reflection;
using Raven.Client.Extensions;
using Raven.Client.Properties;

[assembly: RavenVersion(Build = "42", CommitHash = "a377982", Version = "4.2", FullVersion = "4.2.124-custom-42", ReleaseDateString = "2022-06-07")]

namespace Raven.Client.Properties
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class RavenVersionAttribute : Attribute
    {
        public string CommitHash { get; set; }
        public string Build { get; set; }
        public string Version { get; set; }
        public string FullVersion { get; set; }
        public string ReleaseDateString { get; set; }

        private static int? _buildVersion;
        private static readonly Version _assemblyVersion;
        private static string _assemblyVersionAsString;

        public static readonly RavenVersionAttribute Instance;

        static RavenVersionAttribute()
        {
            _assemblyVersion = typeof(RavenVersionAttribute).GetTypeInfo().Assembly.GetName().Version;
            Instance = (RavenVersionAttribute)typeof(RavenVersionAttribute).GetTypeInfo().Assembly.GetCustomAttributes(typeof(RavenVersionAttribute)).Single();
        }

        public RavenVersionAttribute()
        {
            MajorVersion = _assemblyVersion.Major;
            MajorVersionAsChar = char.Parse(MajorVersion.ToInvariantString());
            MinorVersion = _assemblyVersion.Minor;
            PatchVersion = _assemblyVersion.Build;
        }

        public string AssemblyVersion => _assemblyVersionAsString ?? (_assemblyVersionAsString = $"{MajorVersion.ToInvariantString()}.{MinorVersion.ToInvariantString()}.{PatchVersion.ToInvariantString()}.{BuildVersion.ToInvariantString()}");

        public readonly int MajorVersion;

        internal readonly char MajorVersionAsChar;

        public readonly int MinorVersion;

        public readonly int PatchVersion;

        internal DateTime ReleaseDate => DateTime.Parse(ReleaseDateString);

        public int BuildVersion
        {
            get
            {
                if (_buildVersion == null)
                {
                    if (string.IsNullOrWhiteSpace(Build))
                        throw new ArgumentNullException(nameof(Build));

                    _buildVersion = int.TryParse(Build, out var buildVersion)
                        ? buildVersion
                        : 42;
                }

                return _buildVersion.Value;
            }
        }
    }
}
