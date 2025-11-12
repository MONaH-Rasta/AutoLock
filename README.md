# AutoLock

Oxide plugin for Rust. Automatically adds a codelock to a lockable entity with a set pin

To hide codelock messages, users will have to turn streamer mode on.

## Permissions

* `autolock.use` - Allows users to use the command and features
* `autolock.item.bypass` - Bypass the item requirement

## Chat Commands

* `/al or autolock` - Brings up a help menu
* `/al or autolock toggle` - Toggles automatic codelocks
* `/al or autolock code` - Changes the code of a player

## Configuration

```json
{
  "Disabled Items (Prefabs)": [
    "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab"
  ],
  "Code Lock Expiry Time (Seconds, put -1 if you want to disable)": 10.0,
  "No Escape": {
    "Block Auto Lock whilst in Combat?": true,
    "Block Auto Lock whilst Raid Blocked?": true
  }
}
```

## Localization

```json
{
  "CodeAdded": "Codelock placed with code {0}.",
  "Disabled": "You have disabled auto locks.",
  "Enabled": "You have enabled auto locks.",
  "CodeUpdated": "Your new code is {0}.",
  "NoPermission": "You don't have permission.",
  "InvalidArgs": "/{0} code|toggle",
  "RaidBlocked": "The codelock wasn't automatically locked due to you being raid blocked!",
  "CombatBlocked": "The codelock wasn't automatically locked due to you being combat blocked!"
}
```

## Credits

**FuJiCuRa**, implementation of hidden codelock messages
