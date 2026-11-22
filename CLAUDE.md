# Code conventions

- A virtual method returning Task whose base implementation does nothing is declared async with an empty body, as ServeInventoryToken in GetDataMessage: `protected virtual async Task ServeInventoryToken(Peer peer, Inventory inventory) { }`, never with `return Task.CompletedTask;`.
- In forms and panels, a field's label generally stands to the right of its field, so that the fields stay vertically aligned with each other.
- A form closes with an X button in its top right corner, never with a "Close" button.
