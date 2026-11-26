# Flipper Fusion

Two pinball tables in one Unity project, with a menu to choose between them:

| Flipper | Made by | Scene |
|---|---|---|
| **FORÊT** | Ethan Stasse, Rafael Hoff, Lana Schmitt | `Assets/Scenes/Flipper_Foret.unity` |
| **FORÊT EN FEU** | Damien Rinaldis | `Assets/Scenes/Flipper_ForetBrulee.unity` |

## How to play it

1. Open the project in Unity.
2. Open the scene **`Assets/Scenes/Menu.unity`**. It looks empty in the editor; that's normal, the menu is built by code.
3. Press **Play** and choose a flipper.

You can also open one of the two flipper scenes directly and press Play to test only that table.

## Controls

| Action | Keyboard | Gamepad |
|---|---|---|
| Left flipper | Left arrow / Q / Left Shift | LB / LT |
| Right flipper | Right arrow / D / Right Shift | RB / RT |
| Launch the ball | Space | Start |
| Pause / back | Échap (Esc) | Select |
| Change camera (Forêt en feu) | C | — |

## How a game works

- Enter your name, then play **3 balls**.
- When the ball falls in the drain, you lose it.
- At the end, your score goes into the **top 10** if it is good enough.
- Each flipper has **its own top 10**, saved on the computer (Unity PlayerPrefs).
- The first screen has a **HIGH SCORES** button that shows both top 10s side by side.

## Score display and camera

- The score bar (name, score, ball, record) is always **horizontal at the top of the screen**.
- On **FORÊT EN FEU**, the score is also shown on the backglass.
- On **FORÊT EN FEU**, **C** switches between the player view and the top view. At the start of a game, the message "TOUCHE C : CHANGER DE VUE" is shown for 2.5 seconds as a reminder.
- The FORÊT EN FEU camera **frames the table automatically**: it shows only the table (plus the backglass in player view) and stops just below the score bar, so the bar never covers the game. It reframes itself when the window size changes.
- To tweak it, select the camera and look at the **CameraFlipper** component: **Marge** sets the space around the table, and unticking **Cadrage Auto** goes back to the saved camera positions.

## Points on the FORÊT flipper

| What the ball hits | Points |
|---|---|
| Bumper | 100 |
| Sapin (tree) | 250 |
| Rocher (rock) | 50 |
| Reaching the top of the wooden ramp | 500 |

To change these values, edit the numbers at the top of `Assets/Script/ScoreForet.cs`.

**Ramp help:** once the ball is on the raised part of the wooden ramp, it keeps a minimum speed (3.5 m/s) in the direction it entered. It can't stop halfway and roll back, so it always goes all the way through. Change `vitesseMin` in `Assets/Script/RampeAssistance.cs` to adjust it.

The scoring is added **automatically when the scene starts**: the `Flipper_Foret` scene and its original scripts were not modified.

## Main scripts (in `Assets/Script/`)

| Script | What it does |
|---|---|
| `ProfilFlipper.cs` | Detects which scene is loaded and sets everything up (menu, score, settings). Author names are here. |
| `SelecteurFlipper.cs` | The first screen: choose FORÊT or FORÊT EN FEU, see both high scores. |
| `MenuFlipper.cs` | The in-game menu: name entry, high scores, pause, game over, score display. |
| `GameManager.cs` | Score, balls left, losing the ball, starting a new game. |
| `TableauScores.cs` | Saves and loads the top 10 of each flipper. |
| `ScoreForet.cs` | Adds the scoring to the FORÊT flipper. |
| `ScoreOnHit.cs` | Gives points when the ball touches an object. |
| `RampeScore.cs` | Ramp bonus on the FORÊT flipper. |
| `RampeAssistance.cs` | Keeps the ball moving on the FORÊT ramp so it goes all the way through. |
| `DrainScore.cs` | Makes the FORÊT drain count as a lost ball. |
| `CameraFlipper.cs` | FORÊT EN FEU camera: player / top view (key C) and automatic framing. |

## Good to know (Unity setting)

The project has **Enter Play Mode Options** turned on, with domain and scene reload turned off, so Play starts faster. Because of this, the setup in `ProfilFlipper.cs` also runs directly when Play starts, and not only when a scene loads. If you turn that option off in **Edit > Project Settings > Editor**, everything still works.

## Adding the scenes to a build

The three scenes are already in **File > Build Settings**, with `Menu` first. A built game starts on the choice screen.

## Resetting the high scores

The scores are stored in Unity's PlayerPrefs. To erase everything, use **Edit > Clear All PlayerPrefs** in the Unity editor.
