# CLAUDE.md

This file provides guidance for Claude Code when working on the G-Roll project.

## Project Overview

G-Roll is a Unity mobile game (iOS/Android) built with Clean Architecture, VContainer for dependency injection, and UniTask for async operations.

## Architecture

```
Assets/
├── _Core/           # Shared interfaces, events, optimistic patterns
├── Domain/          # Business logic services
├── Infrastructure/  # Firebase, platform services
├── Presentation/    # UI (Screens, Popups, Components)
├── Gameplay/        # Player, session, scoring systems
└── _Game Assets/    # Unity assets, scenes, legacy code (deprecating)
```

### Key Patterns

- **VContainer DI**: All dependencies injected via `[Inject]` attribute
- **MessageBus**: Cross-module communication via publish/subscribe
- **Optimistic UX**: UI updates immediately, rollback on server failure
- **Lazy<T>**: Used for circular dependency resolution

### LifetimeScope Hierarchy

```
RootLifetimeScope (Boot Scene - DontDestroyOnLoad)
├── AuthSceneLifetimeScope
├── MetaSceneLifetimeScope
└── GameplayLifetimeScope
```

## Build & Run

This is a Unity project. Use Unity Editor to build and run:

- **Unity Version**: Check ProjectSettings for exact version
- **Platform**: iOS and Android
- **Package Manager**: VContainer, UniTask, DOTween

## Common Tasks

### Adding a New Service

1. Define interface in `Assets/_Core/Interfaces/Services/`
2. Implement in `Assets/Domain/` or `Assets/Infrastructure/`
3. Register in appropriate LifetimeScope
4. Inject with `[Inject] private IMyService _service;`

### Adding a New Message

1. Create struct in `Assets/_Core/Events/Messages/`
2. Implement `IMessage` interface
3. Publish: `_messageBus.Publish(new MyMessage(...))`
4. Subscribe: `_messageBus.Subscribe<MyMessage>(handler)`

### Adding a New Screen

1. Create class extending `UIScreenBase` in `Assets/Presentation/`
2. Implement `OnScreenEnterAsync` and `OnScreenExitAsync`
3. Register in NavigationService
4. Navigate: `_navigationService.PushScreenAsync<MyScreen>()`

### Adding a New Popup

1. Create class extending `UIPopupBase` in `Assets/Presentation/`
2. Implement `OnPopupShowAsync`
3. Return result: `CloseWithResult(value)`
4. Show: `await _navigationService.ShowPopupAsync<MyPopup>()`

## Code Style

- **Namespaces**: Follow `GRoll.{Layer}.{Feature}` pattern
- **Interfaces**: Prefix with `I` (e.g., `ISessionService`)
- **Messages**: Suffix with `Message` (e.g., `CurrencyChangedMessage`)
- **Async methods**: Suffix with `Async` and return `UniTask`

## Key Files

| Purpose | Location |
|---------|----------|
| DI Registration | `Assets/Domain/DI/Installers/` |
| Service Interfaces | `Assets/_Core/Interfaces/` |
| Message Types | `Assets/_Core/Events/Messages/` |
| Firebase Gateway | `Assets/Infrastructure/Firebase/` |
| UI Base Classes | `Assets/Presentation/Core/` |

## Testing

- Unity Test Framework for unit tests
- Integration tests via Play Mode tests
- Mock services by implementing interfaces

## Documentation

Detailed documentation in `documentations/`:
- `PROJECT_ARCHITECTURE.md` - Full architecture overview
- `OPTIMISTIC_CLIENT_PHILOSOPHY.md` - Optimistic UX pattern
- `NETWORKED_SERVICES.md` - Firebase integration
- `RENOVATION_JANUARY_28.md` - Recent changes log

## Important Notes

- Never use singletons (Instance pattern) - use VContainer DI
- Always dispose MessageBus subscriptions in OnDestroy
- Use `CompositeDisposable` for multiple subscriptions
- Prefer interfaces over concrete types for dependencies
- Legacy code in `_Game Assets/Scripts/` is being migrated
