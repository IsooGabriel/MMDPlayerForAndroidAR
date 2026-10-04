using MMDPlayerForVR.PmxImporter;
using MMDPlayerForVR.PmxImporter.Builders;
using MMDPlayerForVR.PmxImporter.Parsers;
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

            // Register PmxImporter pipeline parts
            builder.Register<PmxParser>(Lifetime.Transient);
            builder.Register<IPmxMeshBuilder, PmxMeshBuilder>(Lifetime.Transient);
            builder.Register<IPmxBoneBuilder, PmxBoneBuilder>(Lifetime.Transient);
            builder.Register<IPmxMaterialBuilder>
            (
                resolver => new PmxMaterialBuilder(_materialTemplates),
                Lifetime.Transient
            );
            builder.Register<IPmxPhysicsBuilder, PmxPhysicsBuilder>(Lifetime.Transient);

            // Register pipeline
            builder.Register<PmxImporterPipeline>(Lifetime.Transient);

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
