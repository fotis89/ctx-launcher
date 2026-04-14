using wl.Models;

namespace wl.Services;

public class PromptService
{
    public string ResolvePrompt(Workspace ws, string arg)
    {
        var promptFile = Path.Combine(ws.PromptsPath, arg + ".md");
        if (File.Exists(promptFile))
        {
            var content = File.ReadAllText(promptFile);
            var parsed = ParsePromptFile(content, arg);
            return parsed.Body;
        }
        return arg;
    }

    public List<SavedPrompt> ListPrompts(Workspace ws)
    {
        if (!Directory.Exists(ws.PromptsPath))
        {
            return [];
        }

        var prompts = new List<SavedPrompt>();
        foreach (var file in Directory.GetFiles(ws.PromptsPath, "*.md"))
        {
            var content = File.ReadAllText(file);
            var slug = Path.GetFileNameWithoutExtension(file);
            prompts.Add(ParsePromptFile(content, slug));
        }
        return prompts.OrderBy(p => p.Slug).ToList();
    }

    public static SavedPrompt ParsePromptFile(string content, string slug)
    {
        var prompt = new SavedPrompt { Slug = slug, Label = slug };

        content = content.TrimStart();
        if (!content.StartsWith("---"))
        {
            prompt.Body = content.Trim();
            return prompt;
        }

        var secondDash = content.IndexOf("---", 3);
        if (secondDash < 0)
        {
            prompt.Body = content.Trim();
            return prompt;
        }

        var frontmatter = content[3..secondDash].Trim();
        var body = content[(secondDash + 3)..].Trim();

        foreach (var line in frontmatter.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("label:", StringComparison.OrdinalIgnoreCase))
            {
                prompt.Label = trimmed[6..].Trim();
            }
        }

        prompt.Body = body;
        return prompt;
    }
}