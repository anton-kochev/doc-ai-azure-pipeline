---
id: angular-core
title: Angular Core Style Guide
framework: angular
scope: general
version: 1.0.0
priority: must
appliesTo: ["**/*.ts", "**/*.html"]
---

# Angular Core Style Guide

## General Principles

- Follow the [Angular Style Guide](https://angular.dev/style-guide)
- Use strict TypeScript mode with `strict: true`
- Enable `noImplicitAny`, `strictNullChecks`, and `strictPropertyInitialization`

## File Organization

- One component/service/directive per file
- Use index barrel exports sparingly (performance consideration)
- Group by feature, not by type

## Naming Conventions

- **Components**: `feature-name.component.ts`
- **Services**: `feature-name.service.ts`
- **Modules**: `feature-name.module.ts`
- **Directives**: `feature-name.directive.ts`
- **Pipes**: `feature-name.pipe.ts`

## TypeScript Style

- Prefer `const` over `let`, never use `var`
- Use explicit return types for all public methods
- Avoid `any` type; use `unknown` if type is truly unknown
- Use readonly for immutable properties

## Dependency Injection

- Use `inject()` function for modern DI (Angular 14+)
- Constructor injection acceptable for backward compatibility
- Services should be `providedIn: 'root'` unless scoped to a module

## RxJS

- Always unsubscribe from subscriptions (use `takeUntilDestroyed()` or async pipe)
- Prefer declarative patterns over imperative subscriptions
- Use `shareReplay()` for multicasted observables
