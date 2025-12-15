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

    /// <summary>
    /// System prompt for the normalize phase.
    /// </summary>
    public const string NormalizeV1SystemPrompt = """
        You are a recipe standardization assistant. Your task is to analyze a recipe and suggest JSON Patch operations to normalize and improve the recipe data while preserving the original meaning.

        Rules:
        1. Output ONLY valid JSON matching the specified schema. No markdown, no explanations.
        2. Suggest improvements in order of risk: low-risk formatting first, then medium-risk data changes, then high-risk content changes.
        3. Be conservative - prefer fewer high-quality changes over many minor tweaks.
        4. Always include a clear reason for each patch operation.
        5. Flag uncertain changes as high risk for human review.
        6. If no normalizations are needed, return an empty patches array.
        """;

    /// <summary>
    /// User prompt template for the normalize phase (Scriban syntax).
    /// </summary>
    public const string NormalizeV1UserPromptTemplate = """
        Analyze the following recipe and suggest JSON Patch operations to normalize and improve it.

        **Current Recipe:**
        ```json
        {{ recipe | json }}
        ```

        **Focus Areas:**
        {{ if focus_areas }}
        {{ for area in focus_areas }}
        - {{ area }}
        {{ end }}
        {{ else }}
        - All applicable normalizations
        {{ end }}

        **Output Schema:**
        ```json
        {
          "patches": [
            {
              "op": "replace|add|remove",
              "path": "/json/pointer/path",
              "value": "new value (omit for remove)",
              "riskCategory": "low|medium|high",
              "reason": "Explanation for this change"
            }
          ],
          "summary": "Brief summary of all changes",
          "hasHighRiskChanges": true|false
        }
        ```

        **Risk Categories:**
        - **low**: Safe formatting changes (capitalization, punctuation, unit standardization)
        - **medium**: Data modifications that preserve meaning (ingredient parsing, metadata inference)
        - **high**: Content changes affecting cooking (instruction modifications, error corrections)

        **Guidelines:**
        1. Normalize capitalization (title case for name, sentence case for descriptions)
        2. Standardize units (e.g., "tbsp" ? "tablespoon", "c" ? "cup")
        3. Extract preparation notes to separate field (e.g., "onion, diced" ? notes: "diced")
        4. Infer missing metadata if clearly identifiable (cuisine, dietType)
        5. Fix obvious parsing errors
        6. DO NOT change the essential meaning or cooking process

        Output ONLY the JSON object, no additional text or markdown code blocks.
        """;

    /// <summary>
    /// The required variables for the normalize prompt.
    /// </summary>
    public static readonly string[] NormalizeRequiredVariables = ["recipe"];

    /// <summary>
    /// The optional variables for the normalize prompt.
    /// </summary>
    public static readonly string[] NormalizeOptionalVariables = ["focus_areas"];

    /// <summary>
    /// The JSON schema for the normalize patch response.
    /// </summary>
    public const string NormalizePatchSchema = """
        {
          "type": "object",
          "required": ["patches", "summary", "hasHighRiskChanges"],
          "properties": {
            "patches": {
              "type": "array",
              "items": {
                "type": "object",
                "required": ["op", "path", "riskCategory", "reason"],
                "properties": {
                  "op": { "type": "string", "enum": ["replace", "add", "remove"] },
                  "path": { "type": "string", "description": "JSON Pointer path" },
                  "value": { "description": "New value for replace/add operations" },
                  "riskCategory": { "type": "string", "enum": ["low", "medium", "high"] },
                  "reason": { "type": "string", "description": "Explanation for change" }
                }
              }
            },
            "summary": { "type": "string", "description": "Brief summary of all changes" },
            "hasHighRiskChanges": { "type": "boolean", "description": "Whether any high-risk changes are included" }
          }
        }
        """;
}
