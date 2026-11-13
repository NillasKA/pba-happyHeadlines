# Compulsory assignment 2: requirements and evidence

The authoritative brief is [Summarising Happy Headlines](https://moodle.easv.dk/mod/assign/view.php?id=397622). The displayed deadline is **Monday, 16 November 2026, 8:00 AM**. The [course evaluation](https://moodle.easv.dk/mod/questionnaire/view.php?id=396025) precedes access to the assignment. The [week 46 lecture](https://easv.cloud.panopto.eu/Panopto/Pages/Viewer.aspx?id=9cc16a7e-1ef8-4dd6-a7dd-b4fa007ad2d7) allows group submissions and describes using Add submission with a short message. A passing grade is required.

This document maps requirements to project evidence. A repository file is not by itself proof of a successful runtime demonstration or a Moodle submission. The captured screenshots and runtime checks are linked below. The [hand-in guide](hand-in.md) lists the fields and outstanding personal steps.

| Official requirement | Project evidence to inspect | Hand-in evidence still required |
|---|---|---|
| Public GitHub repository | Repository and visibility settings | [Public repository URL](https://github.com/NillasKA/pba-happyHeadlines) |
| C4 level 2 and ArticleService level 3 | [Structurizr model](../architecture/workspace.dsl), `containers` and `article-components` views | [Level 2](screenshots/c4-containers.png) and [ArticleService level 3](screenshots/c4-article-components.png) screenshots |
| Z-axis ArticleService database coordination | `src/Shared/Configuration/ArticleShardRouter.cs`, `src/ArticleService/Infrastructure/ArticleRepository.cs`, `compose.yaml` | Coordinator code in the assignment field |
| X-axis ArticleService scaling and load balancer | `compose.yaml`, `infra/nginx/default.conf` | Scaling code/configuration and explanation of actual routing |
| Circuit breaker in CommentService for ProfanityService unavailability; relation to swimlanes | `src/CommentService/Services/ProfanityClient.cs`, service configuration and tests | Circuit breaker code and swimlane explanation |
| Monitoring examples of metrics, logging and distributed tracing | `infra/grafana`, `infra/prometheus`, `src/Shared/Services/`, Seq and Zipkin | [Metrics](screenshots/metrics-prometheus.png), [logging](screenshots/logging-seq.png), and [tracing](screenshots/tracing-zipkin.png) screenshots; descriptions in the hand-in guide |
| CI and CD pipeline for all services | `.github/workflows/delivery.yml`, `.github/workflows/promote.yml` | CI and CD code in separate fields; validate actual runs and publication separately |
| SubscriberService behind a feature toggle | `src/NewsletterService/Services/NewsletterService.cs`, `src/Shared/Services/SubscriberFeature.cs`, `src/NewsletterService/Infrastructure/SubscriberOutbox.cs`, `src/SubscriberService/`, `compose.yaml` | NewsletterService toggle decision code |
| At least three GAF tactical improvements | [Sustainability evidence](sustainability.md) | [Three measured examples](sustainability.md), each with a screenshot and explanation |
| Video tour, no more than five minutes | Running system and submitted artifacts | Record and submit a tour using course vocabulary; select the most interesting areas if time is short |

The [week 44 lecture](https://easv.cloud.panopto.eu/Panopto/Pages/Viewer.aspx?id=4e68e8ce-8964-44de-9163-b4ec0084296f) describes a SubscriberService layer with its own database and queue, a separate swimlane from NewsletterService, a toggle for the subscriber setup, and inclusion of new services in CI/CD. FeatureHub is an example rather than a mandated product. The [week 45 lecture](https://easv.cloud.panopto.eu/Panopto/Pages/Viewer.aspx?id=3255b1da-d700-46d0-a159-b4f3007ba280) asks for three GAF tactics not already worked on and before/after code documentation. The final assignment brief governs the required hand-in.

## Status

[Verification run 43422690611](https://github.com/NillasKA/pba-happyHeadlines/actions/runs/43422690611) passed the build, 11 unit tests, integration and resilience smoke tests; its [manifest](screenshots/manifest.json), [smoke log](screenshots/smoke.log), [resilience log](screenshots/resilience.log), [measurements](screenshots/gaf-measurements.log), and screenshots record the tested revision and results. The public repository, diagrams, monitoring images and three sustainability comparisons are available for the hand-in. The CD publication and remote deployment workflows are configured but have not been executed. The remaining personal steps are completing the Moodle fields, uploading the images, recording a video of no more than five minutes, and submitting the assignment.
