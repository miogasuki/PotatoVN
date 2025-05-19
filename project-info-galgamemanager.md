# GalgameManager Client (PotatoVN) - Detailed Knowledge Base

This document provides a detailed overview of the `GalgameManager` client application, which is the primary user-facing component of the PotatoVN project. It is intended for AI agents and developers needing a deeper understanding of the client's architecture, features, and key code areas.

## 1. Overview

*   **Component Name:** `GalgameManager`
*   **Type:** Client Application (Desktop)
*   **UI Framework:** WinUI 3
*   **Primary Role:** To provide a convenient game management platform for visual novel enthusiasts.
*   **Part of Solution:** `GalgameManager.sln` (Project Path: `GalgameManager/GalgameManager.csproj`)

## 2. Core Features

The client application implements the core functionalities of PotatoVN:

*   **Game Discovery:** Automatically searches specified folders to find visual novel games.
*   **Information Fetching:** Retrieves game metadata (details, cover art, etc.) from multiple online databases (currently supports Bangumi and Visual Novel Database).
*   **Status Synchronization:** Synchronizes game play status (e.g., playing, completed, planned) with user accounts on supported platforms (e.g., Bangumi).
*   **Cloud Save Sync (Conceptual):** Facilitates tracking of game save locations. Actual synchronization to cloud storage relies on third-party sync software (e.g., OneDrive, NextCloud) monitoring the designated save folders.
*   **Playtime Tracking:** Monitors and records the time spent playing games.
*   **Automated Game Processing:** Can extract games from compressed archives, attempt to identify them, and add them to the user's library.

## 3. Architecture and Technology

*   **Programming Language:** C#
*   **Framework:** .NET
*   **UI Technology:** WinUI 3 (Windows UI Library 3)
*   **Architectural Pattern:** MVVM (Model-View-ViewModel)
    *   **Models:** Represent the application's data and business logic (found in `GalgameManager/Models/` and potentially `GalgameManager.Core/Models/`).
    *   **Views:** Define the UI structure and appearance (XAML files in `GalgameManager/Views/`).
    *   **ViewModels:** Act as intermediaries between Views and Models, handling UI logic and state (found in `GalgameManager/ViewModels/`).
*   **Development Framework Base:** Initially generated using TemplateStudio, providing a standardized project structure and common patterns.
*   **Localization:** Supports multiple languages through localization files, managed via Crowdin (configuration in `crowdin.yml` at the repository root). String resources are located in `GalgameManager/Strings/` within language-specific subfolders (e.g., `zh-CN`, `en-US`), typically in `Resources.resw` files.
    *   **Implementing XAML Localization:**
        *   Use the `x:Uid` attribute on XAML elements to mark them for localization. For example: `<TextBlock x:Uid="MyUniqueControlUid" />`.
        *   In the `.resw` resource file (e.g., `Strings/zh-CN/Resources.resw`), create a `<data>` entry where the `name` attribute is the `x:Uid` value followed by a dot and the target property name. For instance, to set the `Text` property of the `TextBlock` above, the resource key would be `MyUniqueControlUid.Text`.
        *   Example:
            *   XAML: `<TextBlock x:Uid="EditPlayTimeDialog_PlayCountLabel" />`
            *   `Resources.resw` entry:
                ```xml
                <data name="EditPlayTimeDialog_PlayCountLabel.Text" xml:space="preserve">
                  <value>游玩次数:</value>
                </data>
                ```
        *   The application's `GetLocalized()` extension method (found in `GalgameManager.Helpers.StringExtensions.GetLocalized()`) is used in C# code to retrieve localized strings, e.g., `Title = "EditPlayTimeDialog_Title".GetLocalized();`. This implies that for C# string localization, the resource key is used directly without a property suffix.

## 4. Key Files and Directories within `GalgameManager/`

This section highlights important files and directories specific to the client application.

*   **`GalgameManager.csproj`**: The MSBuild project file. Defines dependencies (NuGet packages, project references), build configurations, and included files for the client application.
*   **`App.xaml` / `App.xaml.cs`**:
    *   `App.xaml`: Declares application-level resources and styles.
    *   `App.xaml.cs`: The application's entry point. Handles application lifecycle events (startup, activation, suspension), initializes services, and sets up the main window.
*   **`MainWindow.xaml` / `MainWindow.xaml.cs`**:
    *   `MainWindow.xaml`: Defines the XAML structure for the main application window.
    *   `MainWindow.xaml.cs`: Contains the code-behind logic for the main window, including event handlers and interaction with ViewModels.
*   **`appsettings.json`**: Configuration file for the client application. May store settings like API keys (if not user-specific), default paths, feature flags, etc.
*   **`ViewModels/`**: Contains ViewModel classes that drive the application's UI logic and data binding. These ViewModels often orchestrate interactions with dialogs for editing specific pieces of data (e.g., `PlayedTimeViewModel.cs` launching `EditPlayTimeDialog`).
*   **`Views/`**: Contains XAML files defining the user interface pages and controls. Each View typically corresponds to a ViewModel.
    *   **`Views/Dialog/`**: This subdirectory commonly houses `ContentDialog` XAML files used for focused editing tasks or user prompts (e.g., `EditPlayTimeDialog.xaml` for modifying game play history). These dialogs usually have a corresponding `.xaml.cs` for their logic and are instantiated and shown from ViewModels.
*   **`Models/`**: Contains data model classes representing the entities and data structures used within the client.
    *   **`Galgame.cs`**: A key model representing a game. It includes various properties like `Name`, `ImagePath`, as well as fields for tracking play history such as:
        *   `PlayedTime` (Dictionary<string, int>): Stores individual play sessions, mapping a date string to play duration in minutes.
        *   `PlayCount` (int): Stores the total number of times the game has been played.
        *   `TotalPlayTime` (int): Stores the sum of all play session durations in minutes.
*   **`Services/`**: Houses service classes that encapsulate specific functionalities, such as:
    *   Fetching data from local or remote sources.
    *   File operations.
    *   Navigation within the application.
    *   Interaction with external APIs.
*   **`Helpers/`**: Contains utility classes and extension methods that provide common, reusable functions (e.g., file I/O helpers, string manipulation, UI helpers).
*   **`Contracts/`**: Defines interfaces and data contracts. Interfaces are crucial for decoupling components and enabling testability. Data contracts might define the structure of data exchanged with services or stored locally.
    *   `Contracts/Services/`: Interfaces for service classes.
    *   `Contracts/ViewModels/`: Interfaces for ViewModel classes.
*   **`Assets/`**: Stores static resources used by the application:
    *   `Assets/Images/` or `Assets/Pictures/`: Application icons, default cover art, UI elements.
    *   `Assets/Fonts/`: Custom fonts.
    *   `Assets/Data/`: Potentially default data files or templates.
*   **`Strings/` (e.g., `Strings/en-US/Resources.resw`)**: Contains localized string resources for different languages, enabling internationalization.
*   **`Activation/`**: Includes classes responsible for handling different ways the application can be activated (e.g., normal launch, protocol activation, file association).
    *   `IActivationHandler.cs`: Interface for activation handlers.
    *   Specific handlers like `DefaultActivationHandler.cs`, `BgmOAuthActivationHandler.cs`.
*   **`Behaviors/`**: Contains custom UI behaviors that can be attached to XAML elements to add specific functionalities or modify their behavior without extensive code-behind.
*   **`Enums/`**: Defines enumeration types used throughout the client application for representing sets of named constants (e.g., game status, filter types, page identifiers).
*   **`Styles/`**: May contain XAML resource dictionaries defining common styles and templates for UI controls, ensuring a consistent look and feel. (e.g., `Resource.xaml`)
*   **`Usings.cs`**: Often used in newer C# projects for global using directives to reduce boilerplate in individual files.

## 5. Interaction with Other Components

*   **`GalgameManager.Core`**: The client heavily relies on this library for shared business logic, data models, and core services that might also be used by other parts of the PotatoVN ecosystem (like the server, if applicable for certain models/contracts).
*   **`GalgameManager.Server`**: The client interacts with this server component for features like data synchronization and backup via its RESTful API.
*   **External Databases (Bangumi, VNDB):** The client fetches game information directly from these online databases.
*   **Cloud Sync Software (OneDrive, etc.):** The client manages game save paths, but the actual file synchronization to the cloud is handled by external software chosen by the user.

## 6. Potential Areas for AI Agent Interaction/Analysis

*   **Code Generation/Modification:** Understanding the MVVM structure is key for adding new views, viewmodels, or modifying existing ones.
*   **Feature Implementation:** New features would likely involve creating or updating services, viewmodels, and views.
*   **Bug Fixing:** Debugging would require navigating through the MVVM layers and understanding data flow.
*   **UI/UX Enhancements:** Changes to XAML in `Views/` and potentially `Styles/`.
*   **Localization:** Adding new languages would involve updating files in `Strings/` and ensuring Crowdin integration.
*   **API Integration:** Modifying or adding interactions with external services (Bangumi, VNDB, `GalgameManager.Server`) would typically occur in `Services/` or dedicated API helper classes.
*   **Configuration Management:** Understanding `appsettings.json` for client-side settings.

This document provides a foundational knowledge base. For specific implementation details, direct code analysis of the mentioned files and directories will be necessary.
