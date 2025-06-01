﻿using Amazon.BedrockRuntime;
using Azure.AI.Inference;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using DBChatPro.Models;
using Microsoft.Extensions.AI;
using OpenAI;
using System.Text;
using System.Text.Json;

namespace DBChatPro.Services
{
    // Use this constructor if you're using vanilla OpenAI instead of Azure OpenAI
    // Make sure to update your Program.cs as well
    //public class OpenAIService(OpenAIClient aiClient)d

    public class AIService(IConfiguration config, IServiceProvider serviceProvider)
    {
        IChatClient aiClient;

        public async Task<AIQuery> GetAISQLQuery(string aiModel, string aiService, string userPrompt, DatabaseSchema dbSchema, string databaseType)
        {
            if (aiClient == null)
            {
                aiClient = CreateChatClient(aiModel, aiService);
            }

            List<ChatMessage> chatHistory = new List<ChatMessage>();
            var builder = new StringBuilder();
            var maxRows = config.GetValue<string>("MAX_ROWS");

            builder.AppendLine("Your are a helpful, cheerful database assistant. Do not respond with any information unrelated to databases or queries. Use the following database schema when creating your answers:");

            foreach(var table in dbSchema.SchemaRaw)
            {
                builder.AppendLine(table);
            }

            builder.AppendLine("These tables model the musical performances of Jerry Garcia and the Grateful Dead.");
            builder.AppendLine("Each performance is an Event, but studio sessions and rehearsals are also Events, the type code can be used to differentiate between them.");
            builder.AppendLine("The term 'setlist' is used to refer to a collection of songs performed during a specific event presented in the order they were performed, not including event sets that are soundchecks.");
            builder.AppendLine("A setlist is presented as a comma separated list of song names.");
            builder.AppendLine("The columns selected should include event.date, act.name, and venue.name, along with any other requested fields. Do not return any columns not requested.");
            builder.AppendLine("When you are returning columns that include event date, order by event date descending unless specifically instructed to sort some other way.");

            builder.AppendLine("Include column name headers in the query results.");
            builder.AppendLine("Always provide your answer in the JSON format below:");
            builder.AppendLine(@"{ ""summary"": ""your-summary"", ""query"":  ""your-query"" }");
            builder.AppendLine("Output ONLY JSON formatted on a single line. Do not use new line characters.");
            builder.AppendLine(@"In the preceding JSON response, substitute ""your-query"" with the database query used to retrieve the requested data.");
            builder.AppendLine(@"In the preceding JSON response, substitute ""your-summary"" with an explanation of each step you took to create this query in a detailed paragraph.");
            builder.AppendLine($"Only use {databaseType} syntax for database queries.");
            builder.AppendLine($"Always limit the SQL Query to {maxRows} rows.");
            builder.AppendLine("Always include all of the table columns and details.");

            // Build the AI chat/prompts
            if (string.IsNullOrEmpty(config.GetValue<string>("OLLAMA_ENDPOINT")))
            {
                // Ollama doesn't play well with system prompts and large context windows, so the main prompt can't be a system prompt when Ollama is enabled
                // This also means we have to disable supplemental chat tab :(
                chatHistory.Add(new ChatMessage(Microsoft.Extensions.AI.ChatRole.System, builder.ToString()));
            }
            else
            {
                chatHistory.Add(new ChatMessage(Microsoft.Extensions.AI.ChatRole.User, builder.ToString()));
            }
            
            chatHistory.Add(new ChatMessage(Microsoft.Extensions.AI.ChatRole.User, userPrompt));

            // Send request to Azure OpenAI model
            var response = await aiClient.GetResponseAsync(chatHistory);
            var responseContent = response.Messages[0].Text.Replace("```json", "").Replace("```", "").Replace("\\n", " ");

            try
            {
                return JsonSerializer.Deserialize<AIQuery>(responseContent);
            }
            catch (Exception e)
            {
                throw new Exception("Failed to parse AI response as a SQL Query. The AI response was: " + response.Messages[0].Text);
            }
        }

        private IChatClient CreateChatClient(string aiModel, string aiService)
        {
            switch (aiService)
            {
                case "AzureOpenAI":
                    var apiKey = config.GetValue<string>("AZURE_OPENAI_API_KEY");
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        var credential = new AzureKeyCredential(apiKey);
                        return new AzureOpenAIClient(
                                new Uri(config.GetValue<string>("AZURE_OPENAI_ENDPOINT")),
                                credential)
                                    .AsChatClient(modelId: aiModel);
                    }
                    else
                    {
                        var defaultCredential = new DefaultAzureCredential();
                        return new AzureOpenAIClient(
                                new Uri(config.GetValue<string>("AZURE_OPENAI_ENDPOINT")),
                                defaultCredential)
                                    .AsChatClient(modelId: aiModel);
                    }
                case "OpenAI":
                    return new OpenAIClient(config.GetValue<string>("OPENAI_KEY"))
                                .AsChatClient(modelId: aiModel);
                case "Ollama":
                    return new OllamaChatClient(config.GetValue<string>("OLLAMA_ENDPOINT"), aiModel);
                case "GitHubModels":
                    return new ChatCompletionsClient(
                            endpoint: new Uri("https://models.inference.ai.azure.com"),
                            new AzureKeyCredential(config.GetValue<string>("GITHUB_MODELS_KEY")))
                                .AsChatClient(aiModel);
                case "AWSBedrock":
                    var bedrockClient = serviceProvider.GetRequiredService<IAmazonBedrockRuntime>();
                    return new AWSBedrockClient(bedrockClient, aiModel);
            }

            return null;
        }

        public async Task<ChatResponse> ChatPrompt(List<ChatMessage> prompt, string aiModel, string aiService)
        {
            if (aiClient == null)
            {
                aiClient = CreateChatClient(aiModel, aiService);
            }

            return (await aiClient.GetResponseAsync(prompt));
        }
    }
}
