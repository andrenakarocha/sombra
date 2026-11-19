package br.com.fiap.sombra.clima.arquitetura;

import static com.tngtech.archunit.lang.syntax.ArchRuleDefinition.classes;
import static com.tngtech.archunit.core.domain.properties.CanBeAnnotated.Predicates.annotatedWith;
import static com.tngtech.archunit.lang.syntax.ArchRuleDefinition.noFields;
import static com.tngtech.archunit.lang.syntax.ArchRuleDefinition.noMethods;
import static com.tngtech.archunit.library.Architectures.layeredArchitecture;
import static com.tngtech.archunit.library.dependencies.SlicesRuleDefinition.slices;

import com.tngtech.archunit.core.importer.ImportOption;
import com.tngtech.archunit.junit.AnalyzeClasses;
import com.tngtech.archunit.junit.ArchTest;
import com.tngtech.archunit.lang.ArchRule;
import jakarta.persistence.Entity;
import org.springframework.data.repository.Repository;
import org.springframework.web.bind.annotation.RestController;

/**
 * As camadas do guia do professor, garantidas no build: domínio · aplicação · infraestrutura · api. O domínio não
 * conhece Spring, HTTP, casos de uso nem repositórios; a API não fala direto com a infraestrutura.
 */
@AnalyzeClasses(packages = "br.com.fiap.sombra.clima", importOptions = ImportOption.DoNotIncludeTests.class)
class ArquiteturaTest {

    private static final String BASE = "br.com.fiap.sombra.clima";

    @ArchTest
    static final ArchRule camadas = layeredArchitecture().consideringOnlyDependenciesInLayers()
            .layer("API").definedBy(BASE + ".api..")
            .layer("Aplicação").definedBy(BASE + ".aplicacao..")
            .layer("Infraestrutura").definedBy(BASE + ".infraestrutura..")
            .layer("Domínio").definedBy(BASE + ".dominio..")
            .whereLayer("API").mayNotBeAccessedByAnyLayer()
            .whereLayer("Aplicação").mayOnlyBeAccessedByLayers("API")
            .whereLayer("Infraestrutura").mayOnlyBeAccessedByLayers("Aplicação")
            .whereLayer("Domínio").mayOnlyBeAccessedByLayers("API", "Aplicação", "Infraestrutura");

    @ArchTest
    static final ArchRule dominioSoConheceJavaEOMapeamento = classes().that().resideInAPackage(BASE + ".dominio..")
            .should().onlyDependOnClassesThat().resideInAnyPackage(BASE + ".dominio..", "java..", "jakarta.persistence..")
            .because("as regras ficam protegidas de HTTP, de SQL e do framework (o mapeamento JPA é só metadado)");

    @ArchTest
    static final ArchRule semCiclos = slices().matching(BASE + ".(*)..").should().beFreeOfCycles();

    @ArchTest
    static final ArchRule controllersSoNaApi = classes().that().areAnnotatedWith(RestController.class)
            .should().resideInAPackage(BASE + ".api..").andShould().haveSimpleNameEndingWith("Controller");

    @ArchTest
    static final ArchRule repositoriosSoNaInfraestrutura = classes().that().areAssignableTo(Repository.class)
            .should().resideInAPackage(BASE + ".infraestrutura..").andShould().haveSimpleNameEndingWith("Repository");

    @ArchTest
    static final ArchRule entidadesSoNoDominio = classes().that().areAnnotatedWith(Entity.class)
            .should().resideInAPackage(BASE + ".dominio..");

    @ArchTest
    static final ArchRule controllerNaoDevolveEntidade = noMethods().that().areDeclaredInClassesThat()
            .areAnnotatedWith(RestController.class)
            .should().haveRawReturnType(annotatedWith(Entity.class))
            .because("o controller fala por DTOs: a entidade nunca vai direto para o JSON");

    @ArchTest
    static final ArchRule dtoNaoCarregaEntidade = noFields().that().areDeclaredInClassesThat().resideInAPackage(BASE + ".api..")
            .should().haveRawType(annotatedWith(Entity.class))
            .because("um DTO com a entidade dentro vazaria campos internos e laços de relacionamento");
}
