$ErrorActionPreference = 'Continue'
$output = dotnet ef database update 20260825120000_MergeBakeryCategoryVariants --project Annap.CoffeeQrOrdering.Infrastructure --startup-project Annap.CoffeeQrOrdering.Web --no-build 2>&1
$output | Select-Object -First 60
exit $LASTEXITCODE