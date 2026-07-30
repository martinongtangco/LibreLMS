using Microsoft.AspNetCore.Http;

namespace LearningLms.Host;

/// <summary>DTOs and constants used by Scorm endpoints in Program.cs.</summary>
public static class ScormHelpers
{
    /// <summary>Request body for setValue endpoint.</summary>
    public record SetValueRequest(string Element, string Value);

    /// <summary>Request body for finish endpoint.</summary>
    public record FinishRequest(string? Exit = null);

    /// <summary>SCORM API JavaScript shim content.</summary>
    public static readonly string ScormApiScriptContent = @"
(function() {
    'use strict';

    var sessionId = window.scormSessionId || '';
    var baseUrl = window.scormBaseUrl || '/api/scorm/session/' + sessionId;

    window.API = {
        LMSInitialize: function() {
            return true;
        },

        LMSFinish: function(exitReason) {
            var exit = exitReason || 'normal';
            fetch(baseUrl + '/finish', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ exit: exit })
            }).catch(function() {
                // Silent fail
            });
            return true;
        },

        LMSGetValue: function(element) {
            var result = '';
            try {
                var url = baseUrl + '/getValue?element=' + encodeURIComponent(element);
                var xhr = new XMLHttpRequest();
                xhr.open('GET', url, false);
                xhr.send();
                if (xhr.status === 200) {
                    var response = JSON.parse(xhr.responseText);
                    result = response.value || '';
                }
            } catch(e) {
                result = '';
            }
            return result;
        },

        LMSSetValue: function(element, value) {
            try {
                var xhr = new XMLHttpRequest();
                xhr.open('POST', baseUrl + '/setValue', false);
                xhr.setRequestHeader('Content-Type', 'application/json');
                xhr.send(JSON.stringify({ element: element, value: String(value) }));
                return xhr.status === 200;
            } catch(e) {
                return false;
            }
        },

        LMSCommit: function() {
            try {
                var xhr = new XMLHttpRequest();
                xhr.open('POST', baseUrl + '/commit', false);
                xhr.setRequestHeader('Content-Type', 'application/json');
                xhr.send();
                return xhr.status === 200;
            } catch(e) {
                return false;
            }
        }
    };

    if (window.parent !== window) {
        window.parent.API = window.API;
    }
})();
";

    /// <summary>Extract student ID from HTTP context (claims only — no hardcoded fallback).</summary>
    public static Guid GetStudentId(HttpContext httpContext)
    {
        var claim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.User.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var parsedGuid))
            return parsedGuid;

        // No demo fallback — rely on [Authorize] to enforce login
        return Guid.Empty;
    }

    /// <summary>
    /// Map raw SCORM 1.2 <c>cmi.core.lesson_status</c> values to human-readable display labels.
    /// Also handles legacy custom values ("in-progress", "abandoned").
    /// Unknown values pass through unchanged.
    /// </summary>
    public static string GetDisplayLabel(string? rawStatus)
    {
        if (string.IsNullOrEmpty(rawStatus))
            return "Not Started";

        return rawStatus.ToLowerInvariant() switch
        {
            "not attempted" => "Not Started",
            "neutral"       => "Not Started",
            "incomplete"    => "In Progress",
            "in-progress"   => "In Progress",  // legacy custom value
            "abandoned"     => "Abandoned",     // legacy custom value
            "completed"     => "Completed",
            "passed"        => "Passed",
            "failed"        => "Failed",
            "browsed"       => "Browsed",
            _               => rawStatus        // defensive: pass through unknown values
        };
    }

    /// <summary>
    /// Format <c>ScoreRaw</c> (nullable double, 0–100) as a human-readable percentage string.
    /// Returns "N/A" when scoreRaw is null.
    /// </summary>
    public static string GetDisplayPercentage(double? scoreRaw)
    {
        if (!scoreRaw.HasValue)
            return "N/A";

        return $"{(int)scoreRaw.Value}%";
    }

    /// <summary>
    /// Return CSS background and text color hints for a status badge, based on SCORM lesson_status.
    /// </summary>
    /// <param name="rawStatus">Raw SCORM lesson_status value (or null).</param>
    /// <returns>Tuple of (backgroundColor, textColor).</returns>
    public static (string BackgroundColor, string TextColor) GetStatusBadgeColors(string? rawStatus)
    {
        if (string.IsNullOrEmpty(rawStatus))
            return ("#f5f5f5", "#666"); // Neutral (Not Started)

        return rawStatus.ToLowerInvariant() switch
        {
            // Success — green
            "completed" => ("#e8f5e9", "#2e7d32"),
            "passed"    => ("#e8f5e9", "#2e7d32"),

            // Warning — orange
            "incomplete" => ("#fff3e0", "#e65100"),
            "in-progress" => ("#fff3e0", "#e65100"), // legacy
            "abandoned"   => ("#fff3e0", "#e65100"),  // legacy (treated as warning, not error)

            // Error — red
            "failed" => ("#ffebee", "#c62828"),

            // Neutral — gray (default for not-started, browsed, unknown)
            _ => ("#f5f5f5", "#666")
        };
    }
}
