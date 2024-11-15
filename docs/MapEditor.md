1. Go to properties of CK3 in Steam and add `-mapeditor` parameter
2. Launch the game making sure the newly created mod is added to the playset and enabled
3. Map Editing:
    1. Repack heightmap
		![screenshot](Screenshot_2024-05-08_214628.png)
	2. Go to Map Objects Editor. Click on any territory in the list and select all with Ctrl+A hotkey
		![screenshot](Screenshot_2024-05-08_214847.png)
	3. Click on `Automatically place...` button
		![screenshot](Screenshot_2024-05-08_215322.png)
	4. Some of the territories failed to add locators properly. Click on Filter all entries that contain errors
	    ![screenshot](Screenshot_2024-05-08_215116.png)
	5. Repeat ii-iv until there are no entries with errors
	6. If some entries won't fix themselves select the entry, check what object fails and click on `Configure Autonudge...` button.
	    ![screenshot](Screenshot_2024-05-08_215624.png)
	Then find settings related to that type of object and tweak them then retry steps ii-iv. Usually changing some distance parameters helps. If that still did not help then select the object and move it by hand. Hopefully there not many of them.
	7. Make any other changes in map editor.
	8. Save all and exit (Alt+F4 if it restarts the game instead of exit)
	    ![screenshot](Screenshot_2024-05-08_220216.png)
4. Remove -mapeditor launch option and run the game