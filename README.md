# Blueprint Pieces

Plugin reads .blueprint files to generate pieces buildable with the hammer.

It will also, generate a Resource Crate to pair with the blueprint piece, as the plugin generates the required resources to fulfill the blueprint costs and uses that to define the recipe cost for the crate.

![](https://i.imgur.com/WnalzJv.gif)

## Files

Plugin will create a new directory in your BepinEx/configs folder: <b>BlueprintPieces</b>

The name of the file will be the name of your new piece prefab. So I would recommend to name your files accordingly.

Blueprint file:
```js
// This entry is ignored as some blueprint makers don't designate user friendly names
#Name: MyBlueprintPrefab
// This entry is added to the piece description to detail creator of blueprint
#Creator:MutantArtCat
// This entry is added to the piece description
#Description:"Small trader's camp."
// this entry is ignored
#Category:MISC
#Terrain
// Shape;positionX;positionY;positionZ;radius;rotation;smoothRadius;PaintType
circle;0;-1.271996;0;13;0;3;Paved
#SnapPoints
//positionX;positionY;positionZ;Name
0.1;0;0.1;Center
#Pieces
// PrefabName;Category;positionX;positionY;positionZ;RotationX;RotationY;RotationZ;RotationW;Data;ScaleX;ScaleY;ScaleZ
goblin_pole_small;10;-0.2286987;-1.600498;5.277832;0;0.9951848;0;-0.09801675;"";1;1;1
```

## Features

- Slow Build : Toggle On, Off; to see your blueprints build overtime or instantly
- Ghost Material : Toggle On, Off; to see blueprints with ghost material or normal
- Step Up/Step Down : More control over placement of blueprint in the Y axis
- Step Increment

1.0.1 - Now supports terrain modifications and snap point entries

## Configurations

Plugin will generate configs for blueprints that are successfully added to the game

- Piece Display Name
- Crate Display Name
- Crafting Stations
- Category

## Server Sync

Plugin will share files using server sync

## Recommended mods

https://thunderstore.io/c/valheim/p/JereKuusela/Infinity_Hammer/

Infinity hammer allows to create blueprints using commands. It is an incredibly powerful admin tool

## Contact information
For Questions or Comments, find <span style="color:orange">Rusty</span> in the Odin Plus Team Discord

[![https://i.imgur.com/XXP6HCU.png](https://i.imgur.com/XXP6HCU.png)](https://discord.gg/v89DHnpvwS)

Or come find me at the [Modding Corner](https://discord.gg/fB8aHSfA8B)

##
If you enjoy this mod and want to support me:
[PayPal](https://paypal.me/mpei)

<span>
<img src="https://i.imgur.com/rbNygUc.png" alt="" width="150">
<img src="https://i.imgur.com/VZfZR0k.png" alt="https://www.buymeacoffee.com/peimalcolm2" width="150">
</span>
