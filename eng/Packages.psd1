@{
    # Dependency order: publish Core before Hosting.
    Packages = @(
        @{
            Project = 'src/ApplicationEvents/ApplicationEvents.Core/ApplicationEvents.Core.csproj'
            PackageId = 'Patware.Diagnostics.ApplicationEvents.Core'
            AssemblyName = 'Patware.Diagnostics.ApplicationEvents.Core'
        }
        @{
            Project = 'src/ApplicationEvents/ApplicationEvents.Hosting/ApplicationEvents.Hosting.csproj'
            PackageId = 'Patware.Diagnostics.ApplicationEvents.Hosting'
            AssemblyName = 'Patware.Diagnostics.ApplicationEvents.Hosting'
        }
    )
}
