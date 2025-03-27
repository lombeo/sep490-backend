# SEP490 Backend

## ActionLog API

The ActionLog API provides CRUD operations for system action logs with optimized caching for high performance.

### Features

- Create, Read, Update, and Delete action logs
- Filtering by log type, date range, and search terms
- Pagination support
- Optimized caching with Redis
- Cache invalidation for data consistency

### Endpoints

- `GET /api/actionlog` - Get paginated list of action logs with filtering
- `GET /api/actionlog/{id}` - Get action log by ID
- `POST /api/actionlog` - Create a new action log
- `PUT /api/actionlog/{id}` - Update an existing action log
- `DELETE /api/actionlog/{id}` - Delete an action log
- `POST /api/actionlog/invalidate-cache` - Invalidate cache for action logs

### Caching Strategy

- Individual action logs are cached with a 30-minute expiration
- List results are cached with query-specific keys
- Write operations (create, update, delete) invalidate relevant caches
- Manual cache invalidation is available through the API

### Performance Optimizations

- Response caching with appropriate cache headers
- In-memory cache for frequently accessed items
- Distributed Redis cache for scalability
- AsNoTracking for read-only queries
- Efficient LINQ queries to avoid N+1 query issues
