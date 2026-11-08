# HelpDesk · IT Help Desk

A local .NET 10 and ASP.NET Core sample that demonstrates retrieval-augmented generation (RAG) over 20 fictional IT help desk questions and answers and a small sample software catalog.

## Run the lesson

Open `HelpDesk/HelpDesk.slnx` in Visual Studio, select **HelpDesk.Api** as the startup project and launch profile, and press F5. OpenAI mode uses the `OPENAI_API_KEY` environment variable inherited by Visual Studio. If you added the variable while Visual Studio was running, restart Visual Studio so it can pick it up.

Choose **HelpDesk.Api (Offline)** to see the policy and catalog answer without an OpenAI call. For a paraphrased question, offline mode shows the best matching policy and flags that it may not fully answer the question. You can also start the default OpenAI mode with `dotnet run --project HelpDesk.Api` and open [the help desk page](http://localhost:5137/).

The sample defaults to `gpt-4o-mini`. Use `OPENAI_MODEL` to choose another OpenAI model that supports Structured Outputs through Chat Completions. [OpenAI Structured Outputs guide](https://developers.openai.com/api/docs/guides/structured-outputs?api-mode=chat)

## Choose a database

- **SQLite (default)** uses `HelpDesk.Api/Data/HelpDesk.db`. If the file has no policies, the app initializes it with the 20 sample policies. At startup it also creates the software catalog table and inserts missing sample entries using `HelpDesk.Api/Data/Seed.Sqlite.sql`. Set `Database__SqlitePath` to use another file. Relative paths resolve from the API's content root. The local database file is excluded from Git.
- **SQL Server** uses the local default instance. Set `Database__Provider=SqlServer` before launching the app, or change `Database:Provider` in `HelpDesk.Api/appsettings.json`. In SSMS, connect to `localhost` with Windows Authentication and run `HelpDesk.Api/Data/Seed.sql` to create the `HelpDesk` database and insert missing sample policies and catalog entries. Run the same script on an existing database to add the catalog. Use `ConnectionStrings__HelpDesk` to connect to a different SQL Server instance or a database already attached to SQL Server Express LocalDB.

The two providers have separate setup scripts. Each reads its own database, and switching providers selects which copy of the policy catalog to use.

## Follow the data

1. `PolicyDbContext.cs` maps the policy table for both EF Core providers. `PolicyStore.cs` normalizes the question into distinct keywords and uses a LINQ query that EF Core translates to SQL. An exact example question takes priority. Otherwise, the query selects the policy with the most matching keywords. A tie or zero matches produces an insufficient-information response without calling OpenAI.
2. For a supported installation question such as “Can I install Blender on my work laptop?”, `PolicyAnswerService.cs` retrieves the installation policy and looks up the complete product name in the local software catalog. If the product is unknown or the catalog is unavailable, the API gives the conditional rule and reports insufficient information.
3. For a verified catalog entry, `OpenAiPolicyModel.cs` sends the question, policy excerpt, and catalog entry to OpenAI. The response attributes the status to the catalog and cites the policy for the installation rule. Offline mode combines the same facts directly.

## Try these questions

| Question | What to check |
| --- | --- |
| Can I install Zoom on my work laptop? | The fictional catalog lists Zoom as approved. |
| Can I install Blender on my work laptop? | The fictional catalog says Blender requires IT approval. |
| Can I install ScreenFlow on my work laptop? | The catalog has no matching entry. The answer gives the conditional rule. |
| How long does it take help desk to reply to my request? | The service policy gives two hours as a typical initial reply. |
| How long does it take IT to approve a software request? | The approval policy gives no fixed timeline. |
| My VPN keeps dropping and will not reconnect. | SQL should find the VPN policy from the keywords. |
| What is on the cafeteria menu? | The app should report no matching policy and skip the model. |

You can also send the examples from `HelpDesk.Api/HelpDesk.http`.

## Run the tests

From the `HelpDesk` folder, run `dotnet test HelpDesk.slnx`. The endpoint tests use a temporary SQLite file and offline model implementations.

To run the same tests against SQL Server, initialize the local database with `HelpDesk.Api/Data/Seed.sql`, then run these commands in PowerShell.

```powershell
$env:Database__Provider = 'SqlServer'
dotnet test HelpDesk.slnx
Remove-Item Env:Database__Provider
```
