# geneva-vending

To edit it, open `geneva-vending.sln` in Visual Studio (or an equivalent).

To build it, run `build.cmd`. To run it, run the following commands to make a symbolic link in your server data directory:

```dos
cd /d [PATH TO THIS RESOURCE]
mklink /d X:\cfx-server-data\resources\[local]\geneva-vending dist
```

Afterwards, you can use `ensure geneva-vending` in your server.cfg or server console to start the resource.

## Features
* Automatic resetting of vending-machines, after three (3) minutes of the vending machine running out of soda cans, it will reset back to it's starting stock.
* Uses modern practices. Statebags, and not extremely horrible code.
* Fairly optimized unless you're standing right next to the vending-machine.
* It's a pretty 1:1 replication of actual vending-machines from vanilla GTA5!
### Planned
* Configuration.
* Cleanup code.

## Preview
[Streamable](https://streamable.com/0v56a7)

## Configuration
Nothing! In the future, I may decided to add a small configuration file.