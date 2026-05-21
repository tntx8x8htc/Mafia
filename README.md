# Mafia Manager App

.NET MAUI Android app for managing a Mafia game.

## Features
- New game setup
- Player count and names
- Editable roles
- Random role spinner
- Private role reveal
- Day speaker-turn manager
- Main speaking timer
- Challenge timer
- One challenge per player per round
- Voting / eliminate / revive player
- Night phase guide
- Big buttons and readable UI
- GitHub Actions APK build included

## Build APK locally
```bash
dotnet workload install maui-android
dotnet publish MafiaManagerApp/MafiaManagerApp.csproj -f net8.0-android -c Release -p:AndroidPackageFormat=apk
```

APK output:
`MafiaManagerApp/bin/Release/net8.0-android/publish/`

## Build APK with GitHub
1. Upload this project to a GitHub repository.
2. Open the repository on GitHub.
3. Go to **Actions**.
4. Run **Build Android APK**.
5. Download artifact **MafiaManagerApp-APK**.
