using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;
using Microsoft.Extensions.Logging;
using FastEndpoints.Swagger;   // <-- add this

namespace Microsoft.eShopWeb.PublicApi.CatalogItemEndpoints;

/// <summary>
/// List Catalog Items (paged)
/// </summary>
public class CatalogItemListPagedEndpoint(
    IRepository<CatalogItem> itemRepository,
    IUriComposer uriComposer,
    AutoMapper.IMapper mapper,
    ILogger<CatalogItemListPagedEndpoint> logger) // <-- added logger
  : Endpoint<ListPagedCatalogItemRequest, ListPagedCatalogItemResponse>
{
    public override void Configure()
    {
        Get("api/catalog-items");
        AllowAnonymous();
    }

    public override async Task<ListPagedCatalogItemResponse> ExecuteAsync(
        ListPagedCatalogItemRequest request, CancellationToken ct)
    {
        await Task.Delay(1000, ct);

        var response = new ListPagedCatalogItemResponse(request.CorrelationId());

        var filterSpec = new CatalogFilterSpecification(request.CatalogBrandId, request.CatalogTypeId);
        int totalItems = await itemRepository.CountAsync(filterSpec, ct);

        var pagedSpec = new CatalogFilterPaginatedSpecification(
            skip: request.PageIndex * request.PageSize,
            take: request.PageSize,
            brandId: request.CatalogBrandId,
            typeId: request.CatalogTypeId);

        var items = await itemRepository.ListAsync(pagedSpec, ct);

        response.CatalogItems.AddRange(items.Select(mapper.Map<CatalogItemDto>));
        foreach (var item in response.CatalogItems)
        {
            item.PictureUri = uriComposer.ComposePicUri(item.PictureUri);
        }

        if (request.PageSize > 0)
        {
            response.PageCount = (int)Math.Ceiling((decimal)totalItems / request.PageSize);
        }
        else
        {
            response.PageCount = totalItems > 0 ? 1 : 0;
        }

        // 🔵 Custom log line -> shows in Application Insights `traces`
        logger.LogInformation(
            "CatalogItemListPaged returned {Count} items | page={Page} size={Size} brand={BrandId} type={TypeId} corr={CorrelationId}",
            response.CatalogItems.Count,
            request.PageIndex,
            request.PageSize,
            request.CatalogBrandId,
            request.CatalogTypeId,
            request.CorrelationId());

        return response;
    }
}
