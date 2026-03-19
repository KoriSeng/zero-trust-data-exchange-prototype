---
type: Task
task_id: TASK-019
title: "Create Audit Log Query and Monitoring Tools"
owner: STK-001
status: "In Progress"
related_milestone: MS-006
---

## Description

Create tools and documentation for querying and analysing audit logs in CloudWatch. This includes CloudWatch Insights queries for common audit scenarios, Terraform-managed metric filters and alarms for error-rate alerting, and a minimal CloudWatch dashboard for at-a-glance prototype health. This is a POC — the goal is useful signal, not a full observability platform.

Two log sources are available:

| Log group | Format | Primary use |
|-----------|--------|-------------|
| `/aws/lambda/${project_name}-${environment}-backend` | .NET `ILogger` structured text | Application errors, audit event types, request context |
| `/aws/apigateway/${project_name}-${environment}-backend` | JSON per-request | HTTP status codes, latency, integration errors |

## Definition of Done

- [ ] CloudWatch Insights queries documented for key audit scenarios
- [ ] Terraform metric filter created for Lambda ERROR-level log lines
- [ ] Terraform CloudWatch alarm created (error spike threshold)
- [ ] Terraform CloudWatch dashboard created with error-count and latency widgets
- [ ] Implementation Notes section in this file serves as the operator runbook

## Implementation Notes

---

### CloudWatch Insights queries

Run these in the CloudWatch Logs Insights console. Select the appropriate log group before executing.

#### 1. All REQUEST_SUBMITTED audit events

**Log group**: `/aws/lambda/${project_name}-${environment}-backend`

```
fields @timestamp, @message
| filter @message like /REQUEST_SUBMITTED/
| sort @timestamp desc
| limit 50
```

#### 2. Failed authentication / authorisation attempts

**Log group**: `/aws/lambda/${project_name}-${environment}-backend`

```
fields @timestamp, @message
| filter @message like /401/ or @message like /403/ or @message like /Unauthorized/ or @message like /Forbidden/
| sort @timestamp desc
| limit 50
```

#### 3. Request approvals and rejections

**Log group**: `/aws/lambda/${project_name}-${environment}-backend`

```
fields @timestamp, @message
| filter @message like /APPROVED/ or @message like /REJECTED/
| sort @timestamp desc
| limit 100
```

#### 4. Slow API calls (response time > 1 second)

**Log group**: `/aws/apigateway/${project_name}-${environment}-backend`

```
fields @timestamp, requestId, httpMethod, routeKey, status, responseLength
| filter responseLength > 0
| parse @message '"responseLatency":*,' as responseLatency
| filter responseLatency > 1000
| sort responseLatency desc
| limit 50
```

#### 5. Errors grouped by endpoint

**Log group**: `/aws/apigateway/${project_name}-${environment}-backend`

```
fields httpMethod, routeKey, status
| filter status >= 400
| stats count(*) as errorCount by httpMethod, routeKey, status
| sort errorCount desc
```

---

### Terraform: metric filter for Lambda ERROR lines

Add to `infrastructure/monitoring.tf` (create the file if it does not exist).

```hcl
resource "aws_cloudwatch_log_metric_filter" "lambda_error_count" {
  name           = "${var.project_name}-${var.environment}-lambda-errors"
  log_group_name = "/aws/lambda/${var.project_name}-${var.environment}-backend"
  pattern        = "ERROR"

  metric_transformation {
    name          = "LambdaErrorCount"
    namespace     = "${var.project_name}/${var.environment}"
    value         = "1"
    default_value = "0"
    unit          = "Count"
  }
}
```

> **Note**: `pattern = "ERROR"` matches any log line containing the literal string `ERROR`, which covers .NET `ILogger` output at `LogLevel.Error` and `LogLevel.Critical`. Adjust the pattern (e.g. `[level = ERROR]`) if the log format changes.

---

### Terraform: alarm for error spike

```hcl
resource "aws_cloudwatch_metric_alarm" "lambda_error_spike" {
  alarm_name          = "${var.project_name}-${var.environment}-lambda-error-spike"
  alarm_description   = "Lambda error rate exceeded threshold — investigate CloudWatch Logs."
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 1
  period              = 300 # 5 minutes
  statistic           = "Sum"
  threshold           = 5
  treat_missing_data  = "notBreaching"

  namespace   = "${var.project_name}/${var.environment}"
  metric_name = "LambdaErrorCount"

  # Optional: wire up an SNS topic for email/Slack notifications
  # alarm_actions = [aws_sns_topic.alerts.arn]
}
```

---

### Terraform: minimal CloudWatch dashboard

Two widgets: Lambda error count (bar) and API Gateway response length as a latency proxy (line).

```hcl
resource "aws_cloudwatch_dashboard" "prototype_overview" {
  dashboard_name = "${var.project_name}-${var.environment}-overview"

  dashboard_body = jsonencode({
    widgets = [
      {
        type   = "metric"
        x      = 0
        y      = 0
        width  = 12
        height = 6
        properties = {
          title  = "Lambda Errors (5-min sum)"
          view   = "bar"
          period = 300
          stat   = "Sum"
          metrics = [
            ["${var.project_name}/${var.environment}", "LambdaErrorCount"]
          ]
          region = var.aws_region
        }
      },
      {
        type   = "metric"
        x      = 12
        y      = 0
        width  = 12
        height = 6
        properties = {
          title  = "API Gateway — Response Length (bytes, avg)"
          view   = "timeSeries"
          period = 60
          stat   = "Average"
          metrics = [
            ["AWS/ApiGateway", "IntegrationLatency",
              "ApiId", var.api_gateway_id,
              "Stage", var.environment]
          ]
          region = var.aws_region
        }
      }
    ]
  })
}
```

> **Variables assumed**: `var.project_name`, `var.environment`, `var.aws_region`, `var.api_gateway_id`. Add `api_gateway_id` to `variables.tf` if it is not already declared, sourcing the value from the `aws_apigatewayv2_api` resource output.
