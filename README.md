# Kanban

A simple WPF Kanban board desktop application written in C# targeting .NET 10 (net10.0-windows).

This repository contains the source code for a lightweight Kanban board app implemented with WPF (Views), MVVM-style ViewModels, and adapter components for persistence and utilities.

## Key features
- Multi-column Kanban board with draggable cards
- Card details, tags, priorities, and story points
- Zoom-to-fit board view and refresh controls
- Import/export and backup utilities (adapter layer)

## Requirements
- Windows 10/11
- .NET 10 SDK
- Visual Studio 2022/2026 or dotnet CLI

## Build and run
1. Clone the repository:

   git clone [Kanban URL](https://github.com/bzquan/Kanban.git)

2. Build and run using dotnet CLI from repository root:

   dotnet build Kanban/Kanban.csproj
   dotnet run --project Kanban/Kanban.csproj

Or open the solution in Visual Studio and run the Kanban project (set as startup project).

Project layout (high level)
- Kanban/ — main WPF application project
- src/ — application source files (Views, ViewModels, Models)
- Adapter/ — persistence and utility adapters (MongoDB)
- TestKanban/ — test projects

## Contributing
Contributions are welcome. Please open an issue to discuss large changes, and submit pull requests with focused commits and tests when appropriate.

## License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Troubleshooting
- If images do not appear at runtime, confirm that the images are present in `Kanban/images` and included as project Resources in Kanban.csproj.
- If build reports duplicate assembly attributes, check SDK-generated assembly info and project settings (GenerateAssemblyInfo).
