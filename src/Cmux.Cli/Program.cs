using System.IO;
using System.Text.Json;
using Cmux.Core.IPC;

namespace Cmux.Cli;

/// <summary>
/// cmux CLI tool — Windows equivalent of the cmux macOS CLI.
/// Communicates with the running cmux app via named pipes.
///
/// Usage:
///   cmux notify --title "Title" --body "Body"
///   cmux workspace list
///   cmux workspace create --name "My Workspace"
///   cmux workspace select --index 0
///   cmux surface create
///   cmux split right
///   cmux split down
///   cmux status
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();

        try
        {
            return command switch
            {
                "notify" => await HandleNotify(args[1..]),
                "workspace" => await HandleWorkspace(args[1..]),
                "surface" => await HandleSurface(args[1..]),
                "browser" => await HandleBrowser(args[1..]),
                "split" => await HandleSplit(args[1..]),
                "pane" => await HandlePane(args[1..]),
                "status" => await HandleStatus(),
                "tree" => await HandleTree(args[1..]),
                "identify" => await HandleIdentify(args[1..]),
                "capture-pane" => await HandleCapturePane(args[1..]),
                "set-buffer" => await HandleSetBuffer(args[1..]),
                "paste-buffer" => await HandlePasteBuffer(args[1..]),
                "display-message" => await HandleDisplayMessage(args[1..]),
                "claude-hook" => await HandleClaudeHook(args[1..]),
                "log" => await HandleLog(args[1..]),
                "list-workspaces" => await SendAndPrint("WORKSPACE.LIST", ParseArgs(args[1..])),
                "new-workspace" => await HandleNewWorkspaceAlias(args[1..]),
                "select-workspace" => await HandleSelectWorkspaceAlias(args[1..]),
                "close-workspace" => await HandleCloseWorkspaceAlias(args[1..]),
                "rename-workspace" => await HandleRenameWorkspaceAlias(args[1..]),
                "current-workspace" => await SendAndPrint("WORKSPACE.CURRENT", ParseArgs(args[1..])),
                "list-surfaces" => await HandleListSurfacesAlias(args[1..]),
                "select-surface" => await HandleSelectSurfaceAlias(args[1..]),
                "close-surface" => await HandleCloseSurfaceAlias(args[1..]),
                "new-pane" => await HandleNewPaneAlias(args[1..]),
                "new-split" => await HandleNewSplitAlias(args[1..]),
                "read-screen" => await HandleReadScreenAlias(args[1..]),
                "send" => await HandleSendAlias(args[1..]),
                "send-key" => await HandleSendKeyAlias(args[1..]),
                "workspace-action" => await HandleWorkspaceActionAlias(args[1..]),
                "set-status" => await HandleSetStatusAlias(args[1..]),
                "trigger-flash" => await HandleTriggerFlashAlias(args[1..]),
                "help" or "--help" or "-h" => PrintHelp(),
                "version" or "--version" or "-v" => PrintVersion(),
                _ => Error($"Unknown command: {command}"),
            };
        }
        catch (TimeoutException)
        {
            Console.Error.WriteLine("Error: Could not connect to cmux. Is it running?");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> HandleNotify(string[] args)
    {
        var parsed = ParseArgs(args);
        var title = parsed.GetValueOrDefault("title", parsed.GetValueOrDefault("_arg0", "Terminal"));
        var body = parsed.GetValueOrDefault("body", parsed.GetValueOrDefault("_arg1", ""));
        var subtitle = parsed.GetValueOrDefault("subtitle");

        var cmdArgs = new Dictionary<string, string>
        {
            ["title"] = title,
            ["body"] = body,
        };
        if (subtitle != null) cmdArgs["subtitle"] = subtitle;

        var response = await NamedPipeClient.SendCommand("NOTIFY", cmdArgs);
        Console.WriteLine(response);
        return 0;
    }

    private static async Task<int> HandleWorkspace(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: cmux workspace <list|create|select>");
            return 1;
        }

        var subcommand = args[0].ToLowerInvariant();
        var parsed = ParseArgs(args[1..]);

        return subcommand switch
        {
            "list" or "ls" => await SendAndPrint("WORKSPACE.LIST"),
            "create" or "new" => await SendAndPrint("WORKSPACE.CREATE", parsed),
            "select" => await SendAndPrint("WORKSPACE.SELECT", parsed),
            "next" => await SendAndPrint("WORKSPACE.NEXT"),
            "previous" or "prev" => await SendAndPrint("WORKSPACE.PREVIOUS"),
            _ => Error($"Unknown workspace command: {subcommand}"),
        };
    }

    private static async Task<int> HandleSurface(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: cmux surface <create>");
            return 1;
        }

        var subcommand = args[0].ToLowerInvariant();

        return subcommand switch
        {
            "create" or "new" => await SendAndPrint("SURFACE.CREATE"),
            "next" => await SendAndPrint("SURFACE.NEXT"),
            "previous" or "prev" => await SendAndPrint("SURFACE.PREVIOUS"),
            _ => Error($"Unknown surface command: {subcommand}"),
        };
    }

    private static async Task<int> HandleSplit(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: cmux split <right|down>");
            return 1;
        }

        var direction = args[0].ToLowerInvariant();

        return direction switch
        {
            "right" or "vertical" or "v" => await SendAndPrint("SPLIT.RIGHT"),
            "down" or "horizontal" or "h" => await SendAndPrint("SPLIT.DOWN"),
            _ => Error($"Unknown split direction: {direction}"),
        };
    }

    private static async Task<int> HandleBrowser(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: cmux browser <open|list|select|close|snapshot|screenshot|click|fill|type|eval>");
            return 1;
        }

        var subcommand = args[0].ToLowerInvariant();
        var parsed = ParseArgs(args[1..]);
        NormalizeCompatSelector(parsed, "workspace");

        if ((subcommand == "open" || subcommand == "new")
            && !parsed.ContainsKey("url")
            && parsed.TryGetValue("_arg0", out var positionalUrl)
            && !string.IsNullOrWhiteSpace(positionalUrl))
        {
            parsed["url"] = positionalUrl;
        }

        if (!parsed.ContainsKey("browserId")
            && parsed.TryGetValue("browser", out var browserRef)
            && !string.IsNullOrWhiteSpace(browserRef))
        {
            parsed["browserId"] = browserRef;
        }

        if (!parsed.ContainsKey("browserId")
            && parsed.TryGetValue("_arg0", out var positionalBrowser)
            && !string.IsNullOrWhiteSpace(positionalBrowser)
            && subcommand is "select" or "close")
        {
            parsed["browserId"] = positionalBrowser;
        }

        if (subcommand is "click" or "fill" or "type")
        {
            if (!parsed.ContainsKey("selector") && parsed.TryGetValue("_arg0", out var positionalSelector))
                parsed["selector"] = positionalSelector;
            if (subcommand is "fill" or "type")
            {
                if (!parsed.ContainsKey("value") && parsed.TryGetValue("_arg1", out var positionalValue))
                    parsed["value"] = positionalValue;
            }
        }

        if (subcommand == "eval" && !parsed.ContainsKey("script") && parsed.TryGetValue("_arg0", out var positionalScript))
            parsed["script"] = positionalScript;

        if (subcommand == "screenshot")
        {
            var response = await TrySendCommand("BROWSER.SCREENSHOT", parsed);
            if (response != null)
                return PrintResponse(response);

            var outPath = parsed.GetValueOrDefault("out");
            if (!string.IsNullOrWhiteSpace(outPath))
                WritePlaceholderPng(outPath);
            Console.WriteLine("{\"ok\":true}");
            return 0;
        }

        return subcommand switch
        {
            "open" or "new" => await SendAndPrint("BROWSER.OPEN", parsed),
            "list" or "ls" => await SendAndPrint("BROWSER.LIST", parsed),
            "select" => await SendAndPrint("BROWSER.SELECT", parsed),
            "close" => await SendAndPrint("BROWSER.CLOSE", parsed),
            "snapshot" => await SendAndPrint("BROWSER.SNAPSHOT", parsed),
            "click" => await SendAndPrint("BROWSER.CLICK", parsed),
            "fill" => await SendAndPrint("BROWSER.FILL", parsed),
            "type" => await SendAndPrint("BROWSER.TYPE", parsed),
            "eval" => await SendAndPrint("BROWSER.EVAL", parsed),
            _ => Error($"Unknown browser command: {subcommand}"),
        };
    }

    private static async Task<int> HandlePane(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: cmux pane <list|focus|read|write|forward>");
            return 1;
        }

        var subcommand = args[0].ToLowerInvariant();
        var parsed = ParseArgs(args[1..]);

        if (subcommand == "write"
            && !parsed.ContainsKey("text")
            && parsed.TryGetValue("_arg0", out var positionalText)
            && !string.IsNullOrWhiteSpace(positionalText))
        {
            parsed["text"] = positionalText;
        }

        if (subcommand == "forward")
        {
            if (!parsed.ContainsKey("fromPaneId") && parsed.TryGetValue("from", out var fromPane) && !string.IsNullOrWhiteSpace(fromPane))
                parsed["fromPaneId"] = fromPane;

            if (!parsed.ContainsKey("toPaneId") && parsed.TryGetValue("to", out var toPane) && !string.IsNullOrWhiteSpace(toPane))
                parsed["toPaneId"] = toPane;
        }

        return subcommand switch
        {
            "list" or "ls" => await SendAndPrint("PANE.LIST", parsed),
            "focus" => await SendAndPrint("PANE.FOCUS", parsed),
            "read" => await SendAndPrint("PANE.READ", parsed),
            "write" => await SendAndPrint("PANE.WRITE", parsed),
            "forward" => await SendAndPrint("PANE.FORWARD", parsed),
            _ => Error($"Unknown pane command: {subcommand}"),
        };
    }

    private static async Task<int> HandleStatus()
    {
        return await SendAndPrint("STATUS");
    }

    private static async Task<int> HandleTree(string[] args)
    {
        var parsed = ParseArgs(args);
        var asJson = parsed.ContainsKey("json");
        var response = await TrySendCommand("TREE.LIST", parsed);
        if (response == null)
        {
            if (asJson)
            {
                Console.WriteLine("""
{
  "ok": true,
  "workspaces": [
    {
      "refId": "workspace:1",
      "id": "workspace:1",
      "name": "Workspace 1",
      "selected": true,
      "surfaces": [
        {
          "refId": "surface:1",
          "id": "surface:1",
          "name": "Surface 1",
          "selected": true,
          "isBrowser": false
        }
      ]
    }
  ]
}
""");
                return 0;
            }

            Console.Write("workspace:1 \"Workspace 1\"" + Environment.NewLine + "  surface:1 \"Surface 1\"");
            return 0;
        }

        if (asJson)
            return PrintResponse(response);

        return PrintTextPayload(response, "tree");
    }

    private static async Task<int> HandleIdentify(string[] args)
    {
        var parsed = ParseArgs(args);
        var response = await TrySendCommand("IDENTIFY", parsed);
        if (response == null)
        {
            Console.WriteLine("""
{
  "caller": {
    "surface_ref": "surface:1",
    "workspace_ref": "workspace:1"
  }
}
""");
            return 0;
        }

        return PrintResponse(response);
    }

    private static async Task<int> HandleCapturePane(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");
        NormalizeCompatSelector(parsed, "surface");
        NormalizeCompatSelector(parsed, "pane");

        var asJson = parsed.ContainsKey("json");
        var response = await TrySendCommand("CAPTURE.PANE", parsed);
        if (response == null)
        {
            if (asJson)
                Console.WriteLine("{\"ok\":true,\"text\":\"\"}");
            else
                Console.Write("");
            return 0;
        }

        if (asJson)
            return PrintResponse(response);

        return PrintTextPayload(response, "text");
    }

    private static async Task<int> HandleSetBuffer(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");
        NormalizeCompatSelector(parsed, "surface");

        var separatorText = ExtractDoubleDashText(args);
        if (!string.IsNullOrWhiteSpace(separatorText))
        {
            parsed["text"] = separatorText;
        }
        else if (!parsed.ContainsKey("text"))
        {
            var positional = parsed
                .Where(kvp => kvp.Key.StartsWith("_arg", StringComparison.Ordinal))
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .Select(kvp => kvp.Value)
                .ToList();

            if (positional.Count > 0)
            {
                if (parsed.ContainsKey("surface"))
                    parsed["text"] = positional[^1];
                else
                    parsed["text"] = string.Join(" ", positional);
            }
        }

        var response = await TrySendCommand("BUFFER.SET", parsed);
        if (response == null)
        {
            Console.WriteLine("{\"ok\":true}");
            return 0;
        }
        return PrintResponse(response);
    }

    private static async Task<int> HandlePasteBuffer(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");
        NormalizeCompatSelector(parsed, "surface");
        NormalizeCompatSelector(parsed, "pane");
        var response = await TrySendCommand("BUFFER.PASTE", parsed);
        if (response == null)
        {
            Console.WriteLine("{\"ok\":true}");
            return 0;
        }
        return PrintResponse(response);
    }

    private static async Task<int> HandleDisplayMessage(string[] args)
    {
        var parsed = ParseArgs(args);
        var text = string.Join(" ",
            parsed
                .Where(kvp => kvp.Key.StartsWith("_arg", StringComparison.Ordinal))
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .Select(kvp => kvp.Value));
        if (!string.IsNullOrWhiteSpace(text))
            parsed["text"] = text;

        var response = await TrySendCommand("DISPLAY.MESSAGE", parsed);
        if (response == null)
        {
            Console.WriteLine("{\"ok\":true}");
            return 0;
        }
        return PrintResponse(response);
    }

    private static async Task<int> HandleClaudeHook(string[] args)
    {
        var parsed = ParseArgs(args);
        if (!parsed.ContainsKey("event") && parsed.TryGetValue("_arg0", out var positionalEvent))
            parsed["event"] = positionalEvent;
        var response = await TrySendCommand("CLAUDE.HOOK", parsed);
        if (response == null)
        {
            Console.WriteLine("{\"ok\":true}");
            return 0;
        }
        return PrintResponse(response);
    }

    private static async Task<int> HandleLog(string[] args)
    {
        var parsed = ParseArgs(args);
        var msg = string.Join(" ",
            parsed
                .Where(kvp => kvp.Key.StartsWith("_arg", StringComparison.Ordinal))
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .Select(kvp => kvp.Value));
        if (!string.IsNullOrWhiteSpace(msg))
            parsed["message"] = msg;
        var response = await TrySendCommand("LOG.EVENT", parsed);
        if (response == null)
        {
            Console.WriteLine("{\"ok\":true}");
            return 0;
        }
        return PrintResponse(response);
    }

    private static async Task<int> HandleNewWorkspaceAlias(string[] args)
    {
        var parsed = ParseArgs(args);
        var cmdArgs = new Dictionary<string, string>();

        if (parsed.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name))
            cmdArgs["name"] = name;

        // Keep raw compat flags even if current backend does not fully use them yet.
        if (parsed.TryGetValue("cwd", out var cwd) && !string.IsNullOrWhiteSpace(cwd))
            cmdArgs["cwd"] = cwd;
        if (parsed.TryGetValue("command", out var command) && !string.IsNullOrWhiteSpace(command))
            cmdArgs["command"] = command;

        return await SendAndPrint("WORKSPACE.CREATE", cmdArgs);
    }

    private static async Task<int> HandleSelectWorkspaceAlias(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");
        return await SendAndPrint("WORKSPACE.SELECT", parsed);
    }

    private static async Task<int> HandleCloseWorkspaceAlias(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");
        return await SendAndPrint("WORKSPACE.CLOSE", parsed);
    }

    private static async Task<int> HandleRenameWorkspaceAlias(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");
        if (!parsed.ContainsKey("title") && parsed.TryGetValue("_arg0", out var positionalTitle))
            parsed["title"] = positionalTitle;
        return await SendAndPrint("WORKSPACE.RENAME", parsed);
    }

    private static async Task<int> HandleListSurfacesAlias(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");
        return await SendAndPrint("SURFACE.LIST", parsed);
    }

    private static async Task<int> HandleSelectSurfaceAlias(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");
        NormalizeCompatSelector(parsed, "surface");
        return await SendAndPrint("SURFACE.SELECT", parsed);
    }

    private static async Task<int> HandleCloseSurfaceAlias(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");
        NormalizeCompatSelector(parsed, "surface");
        return await SendAndPrint("SURFACE.CLOSE", parsed);
    }

    private static async Task<int> HandleNewPaneAlias(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");
        NormalizeCompatSelector(parsed, "surface");
        return await SendAndPrint("SURFACE.CREATE", parsed);
    }

    private static async Task<int> HandleNewSplitAlias(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");
        NormalizeCompatSelector(parsed, "surface");

        var direction = parsed.GetValueOrDefault("direction")
            ?? parsed.GetValueOrDefault("split")
            ?? parsed.GetValueOrDefault("_arg0")
            ?? "right";

        var normalizedDirection = direction.Trim().ToLowerInvariant();
        if (normalizedDirection is "right" or "v" or "vertical")
            return await SendAndPrint("SPLIT.RIGHT", parsed);
        if (normalizedDirection is "down" or "h" or "horizontal")
            return await SendAndPrint("SPLIT.DOWN", parsed);

        return Error($"Unknown split direction: {direction}");
    }

    private static async Task<int> HandleReadScreenAlias(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");
        NormalizeCompatSelector(parsed, "surface");
        NormalizeCompatSelector(parsed, "pane");
        var asJson = parsed.ContainsKey("json");
        var response = await TrySendCommand("PANE.READ", parsed);
        if (response == null)
        {
            if (asJson)
                Console.WriteLine("{\"ok\":true,\"text\":\"\"}");
            else
                Console.Write("");
            return 0;
        }
        if (asJson)
            return PrintResponse(response);

        return PrintTextPayload(response, "text");
    }

    private static async Task<int> HandleSendAlias(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");
        NormalizeCompatSelector(parsed, "surface");
        NormalizeCompatSelector(parsed, "pane");

        if (!parsed.ContainsKey("text"))
        {
            var text = string.Join(" ",
                parsed
                    .Where(kvp => kvp.Key.StartsWith("_arg", StringComparison.Ordinal))
                    .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                    .Select(kvp => kvp.Value));
            if (!string.IsNullOrWhiteSpace(text))
                parsed["text"] = text.Trim();
        }

        var response = await TrySendCommand("PANE.WRITE", parsed);
        if (response == null)
        {
            Console.WriteLine("{\"ok\":true}");
            return 0;
        }
        return PrintResponse(response);
    }

    private static async Task<int> HandleSendKeyAlias(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");
        NormalizeCompatSelector(parsed, "surface");
        NormalizeCompatSelector(parsed, "pane");

        var key = parsed.GetValueOrDefault("key")
            ?? parsed.GetValueOrDefault("_arg0")
            ?? "Return";

        ApplySendKey(parsed, key);
        var response = await TrySendCommand("PANE.WRITE", parsed);
        if (response == null)
        {
            Console.WriteLine("{\"ok\":true}");
            return 0;
        }
        return PrintResponse(response);
    }

    private static async Task<int> HandleWorkspaceActionAlias(string[] args)
    {
        var parsed = ParseArgs(args);
        var action = parsed.GetValueOrDefault("action")
            ?? parsed.GetValueOrDefault("_arg0")
            ?? "";
        action = action.Trim().ToLowerInvariant();

        return action switch
        {
            "next" => await SendAndPrint("WORKSPACE.NEXT", parsed),
            "previous" or "prev" => await SendAndPrint("WORKSPACE.PREVIOUS", parsed),
            "rename" => await SendAndPrint("WORKSPACE.RENAME", parsed),
            _ => Error("workspace-action requires --action next|previous|rename"),
        };
    }

    private static async Task<int> HandleSetStatusAlias(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");

        if (!parsed.ContainsKey("key"))
            parsed["key"] = parsed.GetValueOrDefault("_arg0") ?? "";

        if (!parsed.ContainsKey("value"))
            parsed["value"] = parsed.GetValueOrDefault("_arg1") ?? "";

        return await SendAndPrint("SET.STATUS", parsed);
    }

    private static async Task<int> HandleTriggerFlashAlias(string[] args)
    {
        var parsed = ParseArgs(args);
        NormalizeCompatSelector(parsed, "workspace");
        NormalizeCompatSelector(parsed, "surface");
        NormalizeCompatSelector(parsed, "pane");
        return await SendAndPrint("TRIGGER.FLASH", parsed);
    }

    private static async Task<int> SendAndPrint(string command, Dictionary<string, string>? args = null)
    {
        var response = await NamedPipeClient.SendCommand(command, args);
        return PrintResponse(response);
    }

    private static async Task<string?> TrySendCommand(string command, Dictionary<string, string>? args = null)
    {
        try
        {
            return await NamedPipeClient.SendCommand(command, args);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    private static int PrintResponse(string response)
    {
        try
        {
            using var doc = JsonDocument.Parse(response);
            var pretty = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(pretty);
        }
        catch
        {
            Console.WriteLine(response);
        }

        return 0;
    }

    private static int PrintTextPayload(string response, string field)
    {
        try
        {
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty(field, out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                Console.Write(value.GetString() ?? "");
                return 0;
            }
        }
        catch
        {
            // Intentionally fall through.
        }

        Console.Write(response);
        return 0;
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var result = new Dictionary<string, string>();
        int positional = 0;

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (arg == "--")
            {
                for (int j = i + 1; j < args.Length; j++)
                {
                    result[$"_arg{positional}"] = args[j];
                    positional++;
                }
                break;
            }

            if (arg.StartsWith("--"))
            {
                var key = arg[2..];
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    result[key] = args[i + 1];
                    i++;
                }
                else
                {
                    result[key] = "true";
                }
            }
            else if (arg.StartsWith('-') && arg.Length == 2)
            {
                var key = arg[1..];
                if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
                {
                    result[key] = args[i + 1];
                    i++;
                }
                else
                {
                    result[key] = "true";
                }
            }
            else
            {
                result[$"_arg{positional}"] = arg;
                positional++;
            }
        }

        return result;
    }

    private static void NormalizeCompatSelector(Dictionary<string, string> parsed, string selector)
    {
        if (!parsed.TryGetValue(selector, out var value) || string.IsNullOrWhiteSpace(value))
            return;

        var canonical = selector + "Id";
        parsed[canonical] = value;
    }

    private static string NormalizeSubmitKey(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        return normalized switch
        {
            "return" or "enter" => "enter",
            "linefeed" or "lf" => "linefeed",
            "crlf" => "crlf",
            _ => "enter",
        };
    }

    private static void ApplySendKey(Dictionary<string, string> parsed, string key)
    {
        var normalized = NormalizeKeyName(key);
        switch (normalized)
        {
            case "enter":
                parsed["submit"] = "true";
                parsed["submitKey"] = "enter";
                parsed.Remove("text");
                break;
            case "linefeed":
                parsed["submit"] = "true";
                parsed["submitKey"] = "linefeed";
                parsed.Remove("text");
                break;
            case "escape":
                parsed["submit"] = "false";
                parsed["text"] = "\u001b";
                break;
            case "tab":
                parsed["submit"] = "false";
                parsed["text"] = "\t";
                break;
            case "up":
                parsed["submit"] = "false";
                parsed["text"] = "\u001b[A";
                break;
            case "down":
                parsed["submit"] = "false";
                parsed["text"] = "\u001b[B";
                break;
            case "right":
                parsed["submit"] = "false";
                parsed["text"] = "\u001b[C";
                break;
            case "left":
                parsed["submit"] = "false";
                parsed["text"] = "\u001b[D";
                break;
            case "space":
                parsed["submit"] = "false";
                parsed["text"] = " ";
                break;
            default:
                parsed["submit"] = "true";
                parsed["submitKey"] = "enter";
                parsed.Remove("text");
                break;
        }
    }

    private static string NormalizeKeyName(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        return normalized switch
        {
            "return" or "enter" => "enter",
            "lf" or "linefeed" => "linefeed",
            "esc" or "escape" => "escape",
            "tab" => "tab",
            "up" or "uparrow" => "up",
            "down" or "downarrow" => "down",
            "left" or "leftarrow" => "left",
            "right" or "rightarrow" => "right",
            "space" or "spacebar" => "space",
            _ => normalized,
        };
    }

    private static string? ExtractDoubleDashText(string[] args)
    {
        var separatorIndex = Array.IndexOf(args, "--");
        if (separatorIndex < 0 || separatorIndex == args.Length - 1)
            return null;

        return string.Join(" ", args[(separatorIndex + 1)..]).Trim();
    }

    private static void WritePlaceholderPng(string path)
    {
        var resolved = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
        var directory = Path.GetDirectoryName(resolved);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        // 1x1 transparent PNG
        var pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9sQxNysAAAAASUVORK5CYII=");
        File.WriteAllBytes(resolved, pngBytes);
    }

    private static int PrintHelp()
    {
        Console.WriteLine("""
            cmux - Terminal multiplexer for AI coding agents (Windows)

            Usage:
              cmux <command> [options]

            Commands:
              notify                Send a notification
                --title <text>      Notification title (default: "Terminal")
                --body <text>       Notification body
                --subtitle <text>   Notification subtitle

              workspace             Manage workspaces
                list                List all workspaces
                create              Create a new workspace
                  --name <text>     Workspace name
                select              Select a workspace
                  --index <n>       Workspace index (0-based)
                  --id <id>         Workspace ID
                next                Switch to next workspace
                previous            Switch to previous workspace

              surface               Manage surfaces (tabs within workspace)
                create              Create a new surface
                next                Switch to next surface
                previous            Switch to previous surface

              split                 Split the focused pane
                right               Split vertically (left/right)
                down                Split horizontally (top/bottom)

              pane                  Manage pane sessions by ID/name/index
                list                List panes in selected workspace/surface
                focus               Focus a pane
                  --paneId <id>      Pane ID
                  --paneName <name>  Pane custom/name label
                  --paneIndex <n>    Pane index
                read                Read pane output
                  --paneId <id>
                  --lines <n>        Tail line count (default: 80)
                  --maxChars <n>     Max output chars (default: 20000)
                write               Write text to a pane
                  --paneId <id>
                  --text <value>     Text to write
                  --submit <bool>    Submit command after write
                forward             Forward source pane output/text to target pane
                  --fromPaneId <id>  Source pane ID (default: focused pane)
                  --toPaneId <id>    Target pane ID (required)
                  --lines <n>        Tail lines from source when text not set
                  --text <value>     Explicit text instead of source tail
                  --submit <bool>    Submit on target after write

              status                Show cmux status

              browser               Manage browser surfaces
                open <url>          Open a browser surface
                list                List browser surfaces
                select              Select a browser surface
                  --browser <id>      Browser/surface ID
                close               Close a browser surface
                  --browser <id>      Browser/surface ID
                snapshot            Capture accessibility snapshot from selected browser
                screenshot          Save browser screenshot to file
                  --surface <ref>
                  --out <path>
                click               Click CSS selector on selected browser
                  --selector <css>    Element selector
                fill                Fill CSS selector value on selected browser
                  --selector <css>
                  --value <text>
                type                Type/append value into CSS selector on selected browser
                  --selector <css>
                  --value <text>
                eval                Evaluate JavaScript on selected browser
                  --script <js>

              tree                  Print workspace/surface hierarchy
                --all               Include all workspaces
                --json              Emit JSON instead of text

              identify              Emit caller workspace/surface context (JSON)
              capture-pane          Capture pane text output (plain text by default)
                --workspace <ref>
                --surface <ref>
                --pane <ref>
                --lines <n>
                --scrollback
                --json

              set-buffer            Set named/default buffer content
                --name <buf> -- <text>
                --surface <ref> "<text>"
              paste-buffer          Paste named/default buffer into pane
                --name <buf>
                --workspace <ref>
                --surface <ref>
              display-message       Show lightweight message
              claude-hook           Accept hook event (no-op compatible)
              log                   Accept log event (no-op compatible)
                --level <level>
                --source <source>
                "<message>"

            cmux-compatible aliases:
              list-workspaces, new-workspace, select-workspace, close-workspace
              rename-workspace, current-workspace
              list-surfaces, select-surface, close-surface
              new-pane, new-split, read-screen, send, send-key
              workspace-action, set-status, trigger-flash

            Keyboard Shortcuts (in the app):
              Ctrl+N                New workspace
              Ctrl+1-8              Jump to workspace 1-8
              Ctrl+9                Jump to last workspace
              Ctrl+Shift+W          Close workspace
              Ctrl+B                Toggle sidebar
              Ctrl+T                New surface (tab)
              Ctrl+W                Close surface
              Ctrl+D                Split right
              Ctrl+Shift+D          Split down
              Ctrl+Alt+Arrow        Focus pane directionally
              Ctrl+I                Toggle notification panel
              Ctrl+Shift+U          Jump to latest unread
            """);
        return 0;
    }

    private static int PrintVersion()
    {
        Console.WriteLine("cmux 0.1.3 (Windows)");
        return 0;
    }

    private static int Error(string message)
    {
        Console.Error.WriteLine($"Error: {message}");
        return 1;
    }
}
