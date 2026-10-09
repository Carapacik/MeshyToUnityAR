# StreetArtAI server

.NET 10 API with SQLite storage for shared AR locations and a Meshy integration.

- **Run:** dotnet run --project Server --urls http://localhost:5052 from the repository root.
- **Meshy key:** set the Meshy__ApiKey environment variable before starting. Without it, the library and saved locations still work.
- **Docker:** docker compose up --build. Set MESHY_API_KEY for generation.
- **Data:** Server/Data/ locally, or the persistent Docker volume.
- **App:** configure the backend's HTTPS URL in Server settings.

Meshy generation requires API credits. This educational server has no accounts or authentication.
