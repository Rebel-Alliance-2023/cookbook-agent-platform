# Repair Paraphrase Prompt Template

## Template ID: ingest.repair.paraphrase.v1
## Phase: Ingest.RepairParaphrase

You are a recipe content editor. Your task is to rephrase the following recipe sections to reduce verbatim similarity with the source while preserving all factual information.

## Context

The extracted recipe content has high similarity with the source website text. To avoid copyright concerns, you need to rewrite the specified sections in your own words.

## Source Text Excerpt (for reference)
```
{{ source_excerpt }}
```

## Sections to Rephrase

{{ for section in sections }}
### {{ section.name }}

**Original Text:**
{{ section.original_text }}

**Similarity Score:** {{ section.similarity_score | math.format "P0" }}
**Token Overlap:** {{ section.token_overlap }} tokens

{{ end }}

## Instructions

1. **Preserve all factual information** - ingredients, quantities, temperatures, times, and techniques must remain accurate
2. **Rephrase in your own words** - change sentence structure, word choices, and phrasing
3. **Maintain clarity** - the rephrased text should be clear and easy to follow
4. **Keep the same meaning** - do not add, remove, or change any cooking information
5. **Use natural language** - the result should read naturally, not like a paraphrase

## Output Format

Return a JSON object with the rephrased sections:

```json
{
  "sections": [
    {
      "name": "Description",
      "rephrased_text": "Your rephrased description here..."
    },
    {
      "name": "Instructions", 
      "rephrased_text": "Your rephrased instructions here..."
    }
  ],
  "notes": "Optional notes about changes made"
}
```

## Important

- Do NOT change ingredient names, quantities, or measurements
- Do NOT change cooking temperatures or times
- Do NOT add new steps or remove existing ones
- Focus on rewriting HOW things are described, not WHAT is described
