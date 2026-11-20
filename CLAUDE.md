# Code conventions

- A virtual method returning Task whose base implementation does nothing is declared async with an empty body, as ServeInventoryToken in GetDataMessage: `protected virtual async Task ServeInventoryToken(Peer peer, Inventory inventory) { }`, never with `return Task.CompletedTask;`.
