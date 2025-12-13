namespace Cookbook.Platform.Shared.Prompts.Templates;

/// <summary>
/// Contains the default prompt templates for the Recipe Ingest Agent.
/// </summary>
public static class IngestPromptTemplates
{
    /// <summary>
    /// The phase identifier for the extract phase.
    /// </summary>
    public const string ExtractPhase = "Ingest.Extract";

    /// <summary>
    /// The phase identifier for the normalize phase.
    /// </summary>
    public const string NormalizePhase = "Ingest.Normalize";

    /// <summary>
    /// The phase identifier for the repair JSON phase.
    /// </summary>
    public const string RepairJsonPhase = "Ingest.RepairJson";

    /// <summary>
    /// The phase identifier for the repair paraphrase phase.
    /// </summary>
    public const string RepairParaphrasePhase = "Ingest.RepairParaphrase";

    /// <summary>
    /// System prompt for the extract phase.
    /// </summary>
    public const string ExtractV1SystemPrompt = """
        You are a recipe extraction assistant. Your task is to extract structured recipe data from web page content.

        Rules:
        1. Output ONLY valid JSON matching the specified schema. No markdown, no explanations.
        2. Extract all recipe information accurately: name, description, ingredients, instructions, timing, and metadata.
        3. DO NOT copy large verbatim blocks from the source. Summarize and rephrase descriptions while preserving meaning.
        4. For ingredients: parse quantity, unit, and name separately. If parsing is uncertain, put the full text in "name" with quantity=0 and unit=null.
        5. For instructions: keep each step concise but complete. Preserve temperatures, times, and key techniques.
        6. Use best-effort values for cuisine, dietType, prepTimeMinutes, cookTimeMinutes, servings. Use 0 or null if unknown.
        7. Generate a unique ID using a slugified version of the recipe name.
        """;

    /// <summary>
    /// User prompt template for the extract phase (Scriban syntax).
    /// </summary>
    public const string ExtractV1UserPromptTemplate = """
        Extract a recipe from the following web page content.

        **Source URL:** {{ url }}

        **Page Content:**
        {{ content }}

        **Output Schema:**
        ```json
        {{ schema }}
        ```

        **Instructions:**
        - Extract the recipe matching the schema above.
        - The "id" should be a URL-safe slug of the recipe name (e.g., "chocolate-chip-cookies").
        - Parse ingredients into structured format with quantity, unit, and name.
        - Keep instructions as an array of step strings.
        - DO NOT include large verbatim blocks. Rephrase and summarize while preserving cooking meaning.
        - If timing information is not available, use 0.
        - Set cuisine and dietType if identifiable, otherwise use null.

        Output ONLY the JSON object, no additional text or markdown code blocks.
        """;

    /// <summary>
    /// The required variables for the extract prompt.
    /// </summary>
    public static readonly string[] ExtractRequiredVariables = ["url", "content", "schema"];

    /// <summary>
    /// The optional variables for the extract prompt.
    /// </summary>
    public static readonly string[] ExtractOptionalVariables = [];

    /// <summary>
    /// The JSON schema for the Recipe model used in extraction.
    /// </summary>
    public const string RecipeJsonSchema = """
        {
          "type": "object",
          "required": ["id", "name"],
          "properties": {
            "id": { "type": "string", "description": "URL-safe slug identifier" },
            "name": { "type": "string", "description": "Recipe name/title" },
            "description": { "type": "string", "description": "Brief recipe description (summarized, not verbatim)" },
            "ingredients": {
              "type": "array",
              "items": {
                "type": "object",
                "required": ["name"],
                "properties": {
                  "name": { "type": "string" },
                  "quantity": { "type": "number", "default": 0 },
                  "unit": { "type": "string", "nullable": true },
                  "notes": { "type": "string", "nullable": true }
                }
              }
            },
            "instructions": {
              "type": "array",
              "items": { "type": "string" },
              "description": "Ordered list of cooking steps"
            },
            "cuisine": { "type": "string", "nullable": true },
            "dietType": { "type": "string", "nullable": true },
            "prepTimeMinutes": { "type": "integer", "default": 0 },
            "cookTimeMinutes": { "type": "integer", "default": 0 },
            "servings": { "type": "integer", "default": 0 },
            "tags": { "type": "array", "items": { "type": "string" } },
            "imageUrl": { "type": "string", "nullable": true }
          }
        }
        """;
}
