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

    /// <summary>Extract student ID from HTTP context (claims or demo fallback).</summary>
    public static Guid GetStudentId(HttpContext httpContext)
    {
        var claim = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.User.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out var parsedGuid))
            return parsedGuid;

        // Demo fallback: use first seeded student
        return Guid.Parse("550e8400-e29b-41d4-a716-446655440001");
    }
}
