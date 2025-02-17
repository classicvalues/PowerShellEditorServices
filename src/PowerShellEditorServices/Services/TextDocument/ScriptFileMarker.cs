﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Management.Automation;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Language;
using Microsoft.PowerShell.EditorServices.Utility;

namespace Microsoft.PowerShell.EditorServices.Services.TextDocument
{
    /// <summary>
    /// Contains details for a code correction which can be applied from a ScriptFileMarker.
    /// </summary>
    public sealed class MarkerCorrection
    {
        /// <summary>
        /// Gets or sets the display name of the code correction.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the ScriptRegion that define the edit to be made by the correction.
        /// </summary>
        public ScriptRegion Edit { get; set; }
    }

    /// <summary>
    /// Defines the message level of a script file marker.
    /// </summary>
    public enum ScriptFileMarkerLevel
    {
        /// <summary>
        /// Information: This warning is trivial, but may be useful. They are recommended by PowerShell best practice.
        /// </summary>
        Information = 0,
        /// <summary>
        /// WARNING: This warning may cause a problem or does not follow PowerShell's recommended guidelines.
        /// </summary>
        Warning = 1,
        /// <summary>
        /// ERROR: This warning is likely to cause a problem or does not follow PowerShell's required guidelines.
        /// </summary>
        Error = 2,
        /// <summary>
        /// ERROR: This diagnostic is caused by an actual parsing error, and is generated only by the engine.
        /// </summary>
        ParseError = 3
    };

    /// <summary>
    /// Contains details about a marker that should be displayed
    /// for the a script file.  The marker information could come
    /// from syntax parsing or semantic analysis of the script.
    /// </summary>
    public class ScriptFileMarker
    {
        #region Properties

        /// <summary>
        /// Gets or sets the marker's message string.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets the ruleName associated with this marker.
        /// </summary>
        public string RuleName { get; set; }

        /// <summary>
        /// Gets or sets the marker's message level.
        /// </summary>
        public ScriptFileMarkerLevel Level { get; set; }

        /// <summary>
        /// Gets or sets the ScriptRegion where the marker should appear.
        /// </summary>
        public ScriptRegion ScriptRegion { get; set; }

        /// <summary>
        /// Gets or sets a optional code corrections that can be applied based on its marker.
        /// </summary>
        public IEnumerable<MarkerCorrection> Corrections { get; set; }

        /// <summary>
        /// Gets or sets the name of the marker's source like "PowerShell"
        /// or "PSScriptAnalyzer".
        /// </summary>
        public string Source { get; set; }

        #endregion

        #region Public Methods

        internal static ScriptFileMarker FromParseError(
            ParseError parseError)
        {
            Validate.IsNotNull(nameof(parseError), parseError);

            return new ScriptFileMarker
            {
                Message = parseError.Message,
                Level = ScriptFileMarkerLevel.Error,
                ScriptRegion = ScriptRegion.Create(parseError.Extent),
                Source = "PowerShell"
            };
        }

        internal static ScriptFileMarker FromDiagnosticRecord(PSObject psObject)
        {
            Validate.IsNotNull(nameof(psObject), psObject);

            // make sure psobject is of type DiagnosticRecord
            if (!psObject.TypeNames.Contains(
                    "Microsoft.Windows.PowerShell.ScriptAnalyzer.Generic.DiagnosticRecord",
                    StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Input PSObject must of DiagnosticRecord type.");
            }

            // casting psobject to dynamic allows us to access
            // the diagnostic record's properties directly i.e. <instance>.<propertyName>
            // without having to go through PSObject's Members property.
            dynamic diagnosticRecord = psObject;
            List<MarkerCorrection> markerCorrections = new();
            if (diagnosticRecord.SuggestedCorrections != null)
            {
                foreach (dynamic suggestedCorrection in diagnosticRecord.SuggestedCorrections)
                {
                    markerCorrections.Add(new MarkerCorrection
                    {
                        Name = suggestedCorrection.Description ?? diagnosticRecord.Message,
                        Edit = new ScriptRegion(
                                diagnosticRecord.ScriptPath,
                                suggestedCorrection.Text,
                                startLineNumber: suggestedCorrection.StartLineNumber,
                                startColumnNumber: suggestedCorrection.StartColumnNumber,
                                startOffset: -1,
                                endLineNumber: suggestedCorrection.EndLineNumber,
                                endColumnNumber: suggestedCorrection.EndColumnNumber,
                                endOffset: -1),
                    });
                }
            }

            string severity = diagnosticRecord.Severity.ToString();
            if (!Enum.TryParse(severity, out ScriptFileMarkerLevel level))
            {
                throw new ArgumentException(
                    $"The provided DiagnosticSeverity value '{severity}' is unknown.",
                    "diagnosticSeverity");
            }

            return new ScriptFileMarker
            {
                Message = diagnosticRecord.Message as string ?? string.Empty,
                RuleName = diagnosticRecord.RuleName as string ?? string.Empty,
                Level = level,
                ScriptRegion = ScriptRegion.Create(diagnosticRecord.Extent as IScriptExtent),
                Corrections = markerCorrections,
                Source = "PSScriptAnalyzer"
            };
        }

        #endregion
    }
}
