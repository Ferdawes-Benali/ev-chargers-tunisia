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
import LocationPicker from "@/components/LocationPicker";

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
    <div className="p-4 max-w-md">
      <h1 className="text-xl font-bold mb-4">{t("submit.title")}</h1>
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <Input placeholder={t("submit.name")} aria-label={t("submit.name")} aria-invalid={!!errors.name} {...register("name")} />
          {errors.name && <p className="text-sm text-destructive">{errorText(errors.name.message, "submit.errors.nameRequired")}</p>}
        </div>

        <div>
          <Input placeholder={t("submit.address")} aria-label={t("submit.address")} {...register("address")} />
        </div>

        <div>
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

        <div>
          <Select
            onValueChange={(v) => setValue("connectorType", v as FormValues["connectorType"], { shouldValidate: true })}
            items={CONNECTOR_TYPES.map((type) => ({ value: type, label: type }))}
          >
            <SelectTrigger aria-label={t("submit.connector")} aria-invalid={!!errors.connectorType}>
              <SelectValue placeholder={t("submit.connector")} />
            </SelectTrigger>
            <SelectContent>
              {CONNECTOR_TYPES.map((type) => (
                <SelectItem key={type} value={type}>{type}</SelectItem>
              ))}
            </SelectContent>
          </Select>
          {errors.connectorType && <p className="text-sm text-destructive">{t("submit.errors.connectorRequired")}</p>}
        </div>

        <div>
          <Input placeholder={t("submit.power")} aria-label={t("submit.power")} aria-invalid={!!errors.powerKw} type="number" {...register("powerKw")} />
          {errors.powerKw && <p className="text-sm text-destructive">{errorText(errors.powerKw.message, "submit.errors.powerInvalid")}</p>}
        </div>

        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? t("submit.submitting") : t("submit.submit")}
        </Button>

        {mutation.isError && (
          <p role="alert" className="text-sm text-destructive">
            {axios.isAxiosError(mutation.error) && mutation.error.response?.status === 401
              ? t("submit.loginRequired")
              : t("errors.generic")}
          </p>
        )}
        {mutation.isSuccess && (
          <p role="status" className="text-sm text-green-600">{t("submit.success")}</p>
        )}
      </form>
    </div>
  );
}
