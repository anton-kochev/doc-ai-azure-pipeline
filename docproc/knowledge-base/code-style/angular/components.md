---
id: angular-components
title: Angular Component Style Rules
framework: angular
scope: structure
version: 1.0.0
priority: must
appliesTo: ["**/*.component.ts"]
---

# Angular Component Style Rules

## Component Structure

Components should follow this order:
1. Decorators (`@Component`)
2. Public properties (inputs, outputs)
3. Private properties
4. Constructor or `inject()` calls
5. Lifecycle hooks (in Angular lifecycle order)
6. Public methods
7. Private methods

## Inputs and Outputs

- Use `input()` and `output()` signals (Angular 17.1+)
- For legacy code: use `@Input()` and `@Output()` decorators
- Prefix output event names with verb (e.g., `userSelected`, `dataLoaded`)

## Template Guidelines

- Keep templates simple; move complex logic to component class
- Use `@if`, `@for`, `@switch` control flow (Angular 17+)
- For legacy: use `*ngIf`, `*ngFor`, `*ngSwitch`
- Avoid inline function calls in templates
- Use `trackBy` for `@for` / `*ngFor` when rendering lists

## Change Detection

- Prefer `OnPush` change detection strategy
- Use signals for reactive state management
- Avoid mutating objects; prefer immutable updates

## Example

```typescript
@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'app-user-card',
  styleUrl: './user-card.component.scss',
  templateUrl: './user-card.component.html',
})
export class UserCardComponent {
  // Inputs
  user = input.required<User>();

  // Outputs
  userSelected = output<User>();

  // Private services
  private readonly logger = inject(LoggerService);

  // Lifecycle
  ngOnInit(): void {
    this.logger.log('UserCard initialized');
  }

  // Public methods
  selectUser(): void {
    this.userSelected.emit(this.user());
  }
}
```
