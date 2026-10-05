using MMDPlayerForVR.PmxImporter;
using MMDPlayerForVR.PmxImporter.Builders;
using MMDPlayerForVR.PmxImporter.Core;
using MMDPlayerForVR.PmxImporter.Parsers;
using MMDPlayerForVR.Pose.Application;
using MMDPlayerForVR.Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MMDPlayerForVR
{
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private PlayerLogService _playerLogService;
        [SerializeField] private PmxRuntimeLoader _pmxRuntimeLoader;
        [SerializeField] private PmxMaterialTemplates _materialTemplates;
        [SerializeField] private FilePickerReceiver _filePickerReceiver;

        protected override void Configure(IContainerBuilder builder)
        {
            // Register UI/Services
            if (_playerLogService != null)
            {
                builder.RegisterComponent(_playerLogService);
            }
            else
            {
                builder.RegisterComponentInHierarchy<PlayerLogService>();
            }

            if (_filePickerReceiver != null)
            {
                builder.RegisterComponent(_filePickerReceiver);
            }
            else
            {
                Debug.Log("ローダーない");
            }

                builder.Register<IStreamingAssetsReader, AsyncFileLoader>(Lifetime.Singleton);

            // Register PmxImporter pipeline parts
            builder.Register<PmxParser>(Lifetime.Transient);
            builder.Register<IPmxMeshBuilder, PmxMeshBuilder>(Lifetime.Transient);
            builder.Register<IPmxBoneBuilder, PmxBoneBuilder>(Lifetime.Transient);
            builder.Register<IPmxMaterialBuilder>
            (
                resolver =>
                {
                    IStreamingAssetsReader streamingAssetsReader = resolver.Resolve<IStreamingAssetsReader>();
                    return new PmxMaterialBuilder(_materialTemplates, streamingAssetsReader);
                },
                Lifetime.Transient
            );
            builder.Register<IPmxPhysicsBuilder, PmxPhysicsBuilder>(Lifetime.Transient);

            // Register pipeline
            builder.Register<PmxImporterPipeline>(Lifetime.Transient);

            // Register Pose dependencies
            builder.Register<MMDPlayerForVR.Pose.Application.IVmdParser, MMDPlayerForVR.Pose.Infrastructure.VmdParser>(Lifetime.Transient);
            builder.Register<MMDPlayerForVR.Pose.Application.ApplyInitialPoseUseCase>(
                resolver =>
                {
                    var reader = resolver.Resolve<IStreamingAssetsReader>();
                    var parser = resolver.Resolve<MMDPlayerForVR.Pose.Application.IVmdParser>();
                    var logService = resolver.Resolve<PlayerLogService>();
                    // IIkSolverはPmxRuntimeLoader内で生成・注入するためここではnull
                    return new MMDPlayerForVR.Pose.Application.ApplyInitialPoseUseCase(reader, parser, logService, null);
                },
                Lifetime.Transient
            );

            // Register target for injection if set
            if (_pmxRuntimeLoader != null)
            {
                builder.RegisterComponent(_pmxRuntimeLoader);
            }
            else
            {
                builder.RegisterComponentInHierarchy<PmxRuntimeLoader>();
            }
        }
    }
}
