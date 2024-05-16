# Udon Vehicle Sync - Sacc Flight Adapter

## Installation

1. Install Saccflight: https://github.com/Sacchan-VRC/SaccFlightAndVehicles/releases/tag/1.64
2. Install VRChat World SDK 3.6
3. Install CyanPlayerObjectPool: https://cyanlaser.github.io/CyanPlayerObjectPool/
4. Install TLP UdonVehicleSync SaccFlightAdapter: https://guribo.github.io/TLP/

## Usage

1. Add `TLP_Essentials` prefab to your scene
2. Add `TLP_SyncOrigin` prefab to your scene and verify it is not rotated and at position 0,0,0
3. Add any Sacc vehicle to your scene
4. Drag and drop the `TLP_UdonVehicleSync_with_SettingsTweaker` onto the Sacc vehicle.
5. Open the prefab and adjust the position of the `SettingsTweaker` menu to be easily usable (disable gameobject if not needed).
6. Enter playmode with client sim enabled and verify that there is no related errors on the console.
7. Upload or test with multiple clients to see it in action.


