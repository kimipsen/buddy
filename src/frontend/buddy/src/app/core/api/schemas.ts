import type { components } from './buddy-api';

// The API contract's schemas, generated from docs/backend/openapi/buddy.json (`task docs:openapi`,
// or `npm run api:types` after the backend regenerated the contract). The services in core/ alias
// their request/response types to these, so a backend DTO change surfaces here as a type error.
export type Schemas = components['schemas'];
