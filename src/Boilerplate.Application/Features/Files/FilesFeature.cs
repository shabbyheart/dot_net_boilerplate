using Boilerplate.Application.Common.Messaging;
using Boilerplate.Application.Features.Files.DeleteFile;
using Boilerplate.Application.Features.Files.DownloadFile;
using Boilerplate.Application.Features.Files.GetFile;
using Boilerplate.Application.Features.Files.ListFiles;
using Boilerplate.Application.Features.Files.UploadFile;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Application.Features.Files;

/// <summary>
/// Registers file storage use cases explicitly (ADR-0004, ADR-0030). Infrastructure provides <see cref="FileSettings"/>
/// and the <see cref="Common.Storage.IFileStorage"/> implementation.
/// </summary>
public static class FilesFeature
{
    public static IServiceCollection AddFilesFeature(this IServiceCollection services)
    {
        services.AddScoped<UploadFileHandler>();
        services.AddScoped<ListFilesHandler>();
        services.AddScoped<GetFileHandler>();
        services.AddScoped<DownloadFileHandler>();
        services.AddScoped<DeleteFileHandler>();

        services.AddScoped<IValidator<UploadFileRequest>, UploadFileValidator>();
        services.AddScoped<IValidator<ListFilesRequest>, ListFilesValidator>();

        services.AddOutboxMessage<DeleteFileContentMessage, DeleteFileContentHandler>(DeleteFileContentMessage.OutboxType);

        return services;
    }
}
