# Code conventions

- A virtual method returning Task whose base implementation does nothing is declared async with an empty body, as ServeInventoryToken in GetDataMessage: `protected virtual async Task ServeInventoryToken(Peer peer, Inventory inventory) { }`, never with `return Task.CompletedTask;`.
- In forms and panels, a field's label generally stands to the right of its field, so that the fields stay vertically aligned with each other.
- A form closes with an X button in its top right corner, never with a "Close" button.

# Rendering pitfalls

- An ItemsControl or a ContentControl clips whatever its content draws beyond its bounds, of any shape and kind, such as a shadow, a glow, a blur or a decoration that sticks out, because ClipToBounds is True on the control itself, not on its template's Border or presenter, even when the control has a ControlTheme of its own. Before letting content draw outside its control's bounds, switch the clipping off by setting ClipToBounds to False on that control, as ThemeBlockchain in MiningView.axaml does for the blocks' ItemsControl, instead of extending the control with margins. A standalone ContentPresenter is no replacement for a ContentControl, because it makes its own DataContext the content, which breaks a binding of its Content to the surrounding DataContext.
- The GUI's three-dimensional shapes, such as the block cubes, are seen from the upper right in a cabinet projection: a front face stays undistorted, and depth recedes at 45 degrees up and to the right at half its length, as ThemeCube in MiningView.axaml draws a depth of 36 as 18 to the right and 18 up. The implied viewer looks along parallel sight lines that come from the upper right at 45 degrees and are tilted about 27 degrees from the line perpendicular to the screen. Where such shapes overlap, the one further right lies in front and is drawn on top of its left neighbor; on the rail, a newer block is drawn on top of the older one.
