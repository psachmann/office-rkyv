namespace OfficeRkyv.Contracts.Users;

public sealed partial class CreateUserCommand : ICommand<Unit>;

public sealed partial class UpdateUserCommand : ICommand<Unit>;

public sealed partial class DeleteUserCommand : ICommand<Unit>;
