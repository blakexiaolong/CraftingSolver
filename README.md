# CraftingSolver

CraftingSolver is an in-progress .NET 6.0 application designed to solve the crafting minigame in the video game Final Fantasy 14.

## Installation

Clone the repository and build it using MSBuild or Visual Studio. As the project is still in development, no releases are available yet.

## Usage

* Enter your crafting stats and recipe data into `./ConsoleSolver/Program.cs`
* Build ConsoleSolver using MSBuild or Visual Studio
* Run the built program ConsoleSolver

Once the solver works, and works quickly, the goal is to have a GuiSolver program that fetches crafting recipes from XivAPI or FFXIV Teamcraft, so users don't need to know things like rlvl of the craft, or the progress & quality dividers.

## Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

## The Problem

The crafting minigame in Final Fantasy 14 involves optimization of several different parameters in order to complete a recipe. The parameters are as follows:
* Progress: Starts at 0, and needs to be increased to the Difficulty of the craft in order to complete the craft. Once Progress reaches the Difficulty, the craft is completed, and no more actions can be taken.
* Quality: Starts at or above 0 based on the Quality of the component materials, and needs to be increased to the MaxQuality of the craft. This parameter is not required for a successful craft, but a higher Quality at the time of craft completion influences the liklihood of producing a High Quality item, which has higher stats and is generally more valuable.
* Durability: Starts at the recipe's Durability Max. Some actions consume Durability, while others increase it. The craft immediately fails if a Step begins and Durability is not above 0.

The Crafter has several stats that influence the effects of their actions:
* Craftsmanship: Increases Progress gain
* Control: Increases Quality Gain
* CP (Crafting Points): Consumed by most Actions (and replenished by others

A successful craft looks like a chain of one or more actions, always ending in an action that increases Progress.

## License
[MIT](https://choosealicense.com/licenses/mit/)

## Acknowledgements
* Square Enix for developing Final Fantasy 14
* The developers of [FFXIV Teamcraft](https://github.com/ffxiv-teamcraft] for their crafting simulator, which mine is heavily based on