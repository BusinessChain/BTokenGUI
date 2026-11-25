# Code conventions

- A virtual method returning Task whose base implementation does nothing is declared async with an empty body, as ServeInventoryToken in GetDataMessage: `protected virtual async Task ServeInventoryToken(Peer peer, Inventory inventory) { }`, never with `return Task.CompletedTask;`.
- In forms and panels, a field's label generally stands to the right of its field, so that the fields stay vertically aligned with each other.
- A form closes with an X button in its top right corner, never with a "Close" button.

# Rendering pitfalls

- An ItemsControl clips whatever its items draw beyond its bounds, of any shape and kind, such as a shadow, a glow, a blur or a decoration that sticks out, because the Fluent theme sets ClipToBounds to True on the ItemsControl itself, not on its template's Border or ItemsPresenter. Before letting an item draw outside its own bounds, switch the clipping off with a style that sets ClipToBounds to False on that ItemsControl, as the style for ItemsControl.blocks in MiningView.axaml does, instead of extending the ItemsControl with margins.
- The GUI's three-dimensional shapes, such as the block cubes, are seen from the upper right. Where such shapes overlap, the one further right lies in front and is drawn on top of its left neighbor; on the rail, a newer block is drawn on top of the older one.
