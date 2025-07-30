When testing packages locally you need to delete the package from the global nuget cache on your computer for projects to get the updated version of the package, if it already has one of the same version.

## Clearing the http cache

Sometimes you end up in a bad situation where you want to fetch a version of a released package before it has become available to the feed.

Even after release it might not show up. That is why you might need to clear the cache.

```
dotnet nuget locals --clear http-cache
```