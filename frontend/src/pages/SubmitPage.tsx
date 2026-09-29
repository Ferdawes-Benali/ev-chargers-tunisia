import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router-dom";
import { useTranslation } from "react-i18next";
import { api } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import axios from "axios";
import { CircleAlert, CircleCheck, MapPin, PlugZap, Send } from "lucide-react";
import LocationPicker from "@/components/LocationPicker";
import SectionCard from "@/components/SectionCard";

// Connector names are standards: never translated
const CONNECTOR_TYPES = ["Type2", "CCS", "CHAdeMO", "Tesla"] as const;

// Messages are translation keys, rendered with t() so they follow the current language
const schema = z.object({
  name: z.string().min(1, "submit.errors.nameRequired").max(120, "submit.errors.nameTooLong"),
  address: z.string().optional(),
  lat: z.coerce.number({ error: "submit.errors.locationRequired" }).min(-90).max(90),
  lng: z.coerce.number({ error: "submit.errors.locationRequired" }).min(-180).max(180),
  connectorType: z.enum(CONNECTOR_TYPES, { error: "submit.errors.connectorRequired" }),
  powerKw: z.coerce.number({ error: "submit.errors.powerInvalid" }).positive("submit.errors.powerPositive"),
});

type FormValues = z.infer<typeof schema>;

export default function SubmitPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { register, handleSubmit, setValue, formState: { errors } } = useForm({
    resolver: zodResolver(schema),
  });

  const mutation = useMutation({
    mutationFn: async (values: FormValues) => {
      const body = {
        name: values.name,
        address: values.address || null,
        lat: values.lat,
        lng: values.lng,
        operatorId: null,
        connectors: [{ type: values.connectorType, powerKw: values.powerKw, count: 1 }],
      };
      return (await api.post("/api/v1/stations", body)).data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["stations"] });
      navigate("/stations");
    },
  });

  const onSubmit = (values: FormValues) => mutation.mutate(values);
  /** Error text for a field: its key translated, or a generic message for unexpected validation errors. */
  const errorText = (message: string | undefined, fallbackKey: string) =>
    t(message?.startsWith("submit.errors.") ? message : fallbackKey);

  return (
    <div className="mx-auto max-w-2xl px-4 pt-6 sm:py-8">
      <div className="mb-6 space-y-1">
        <h1 className="text-2xl font-bold tracking-tight">{t("submit.title")}</h1>
        <p className="text-muted-foreground">{t("submit.subtitle")}</p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
        <SectionCard title={t("submit.sections.location")} description={t("submit.sections.locationHelp")} icon={MapPin}>
          <Field htmlFor="submit-name" label={t("submit.name")} help={t("submit.help.name")}>
            <Input
              id="submit-name"
              className="h-10"
              placeholder={t("submit.placeholders.name")}
              aria-label={t("submit.name")}
              aria-invalid={!!errors.name}
              aria-describedby="submit-name-help"
              {...register("name")}
            />
            {errors.name && <p className="text-sm text-destructive">{errorText(errors.name.message, "submit.errors.nameRequired")}</p>}
          </Field>

          <Field htmlFor="submit-address" label={t("submit.address")}>
            <Input
              id="submit-address"
              className="h-10"
              placeholder={t("submit.placeholders.address")}
              aria-label={t("submit.address")}
              {...register("address")}
            />
          </Field>

          <div className="space-y-1.5">
            <p className="text-sm font-medium">{t("submit.mapLabel")}</p>
            <LocationPicker
              onPick={(lat, lng) => {
                setValue("lat", lat, { shouldValidate: true });
                setValue("lng", lng, { shouldValidate: true });
              }}
            />
            {(errors.lat || errors.lng) && (
              <p className="text-sm text-destructive mt-1">{t("submit.errors.locationRequired")}</p>
            )}
          </div>
        </SectionCard>

        <SectionCard title={t("submit.sections.details")} description={t("submit.sections.detailsHelp")} icon={PlugZap}>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              {/* The trigger is named by its aria-label; this is the visible label */}
              <p className="text-sm font-medium" aria-hidden="true">{t("submit.connector")}</p>
              <Select
                onValueChange={(v) => setValue("connectorType", v as FormValues["connectorType"], { shouldValidate: true })}
                items={CONNECTOR_TYPES.map((type) => ({ value: type, label: type }))}
              >
                <SelectTrigger className="h-10 w-full" aria-label={t("submit.connector")} aria-invalid={!!errors.connectorType}>
                  <SelectValue placeholder={t("submit.placeholders.connector")} />
                </SelectTrigger>
                <SelectContent>
                  {CONNECTOR_TYPES.map((type) => (
                    <SelectItem key={type} value={type}>{type}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {errors.connectorType && <p className="text-sm text-destructive">{t("submit.errors.connectorRequired")}</p>}
            </div>

            <Field htmlFor="submit-power" label={t("submit.power")} help={t("submit.help.power")}>
              <Input
                id="submit-power"
                className="h-10 tabular-nums"
                placeholder={t("submit.placeholders.power")}
                aria-label={t("submit.power")}
                aria-invalid={!!errors.powerKw}
                aria-describedby="submit-power-help"
                type="number"
                {...register("powerKw")}
              />
              {errors.powerKw && <p className="text-sm text-destructive">{errorText(errors.powerKw.message, "submit.errors.powerInvalid")}</p>}
            </Field>
          </div>
        </SectionCard>

        {/* On phones the submit bar stays at the bottom of the screen while scrolling */}
        <div className="sticky bottom-0 z-10 -mx-4 space-y-3 border-t bg-background px-4 py-3 sm:static sm:mx-0 sm:border-0 sm:bg-transparent sm:px-0 sm:pb-0">
          {mutation.isError && (
            <p role="alert" className="flex items-start gap-2 rounded-lg border border-danger/40 bg-danger/10 p-3 text-sm text-destructive">
              <CircleAlert aria-hidden="true" className="mt-px size-4 shrink-0" />
              {axios.isAxiosError(mutation.error) && mutation.error.response?.status === 401
                ? t("submit.loginRequired")
                : t("errors.generic")}
            </p>
          )}
          {mutation.isSuccess && (
            <p role="status" className="flex items-center gap-2 rounded-lg border border-success/40 bg-success/10 p-3 text-sm text-success-ink">
              <CircleCheck aria-hidden="true" className="size-4 shrink-0" />
              {t("submit.success")}
            </p>
          )}
          <Button type="submit" disabled={mutation.isPending} className="h-11 w-full gap-2 text-base sm:w-auto sm:px-6">
            <Send aria-hidden="true" className="rtl:-scale-x-100" />
            {mutation.isPending ? t("submit.submitting") : t("submit.submit")}
          </Button>
        </div>
      </form>
    </div>
  );
}

/** A visible label, the control, and optional helper text below it (id: `<htmlFor>-help`). */
function Field({ htmlFor, label, help, children }: { htmlFor: string; label: string; help?: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1.5">
      <label htmlFor={htmlFor} className="text-sm font-medium">{label}</label>
      {children}
      {help && <p id={`${htmlFor}-help`} className="text-xs text-muted-foreground">{help}</p>}
    </div>
  );
}
