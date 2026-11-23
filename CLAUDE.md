# Code conventions

- A virtual method returning Task whose base implementation does nothing is declared async with an empty body, as ServeInventoryToken in GetDataMessage: `protected virtual async Task ServeInventoryToken(Peer peer, Inventory inventory) { }`, never with `return Task.CompletedTask;`.
- In forms and panels, a field's label generally stands to the right of its field, so that the fields stay vertically aligned with each other.
- A form closes with an X button in its top right corner, never with a "Close" button.

# Rendering pitfalls

- An ItemsControl clips whatever its items draw beyond its bounds, of any shape and kind, such as a shadow, a glow, a blur or a decoration that sticks out. Before letting an item draw outside its own bounds, make sure the drawing still lies inside the ItemsControl, by giving its items panel a margin as large as the overhang; if that room must not take part in the layout, cancel it with a negative margin of the same size on the ItemsControl.
